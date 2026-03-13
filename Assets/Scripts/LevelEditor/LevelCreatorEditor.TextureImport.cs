#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public partial class LevelCreatorEditor
{
    #region TEXTURE IMPORT

    private void GenerateFromTexture()
    {
        var texture = _levelCreator.LevelData.sourceTexture;
        if (texture == null)
            return;

        // Ensure texture is readable
        string texturePath = AssetDatabase.GetAssetPath(texture);
        var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
        if (importer != null && !importer.isReadable)
        {
            importer.isReadable = true;
            AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceUpdate);
            texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            _levelCreator.LevelData.sourceTexture = texture;
        }

        RecordUndo("Generate From Texture");

        int texWidth = texture.width;
        int texHeight = texture.height;

        // Clear existing targets and palette
        _levelCreator.LevelData.targetDataList.Clear();
        _levelCreator.LevelData.colorPalette.Clear();

        // Read all pixels
        Color32[] pixels = texture.GetPixels32();

        // Grid dimensions stay as user-set values (no auto-override)
        int gridW = _levelCreator.LevelData.targetAreaWidth;
        int gridH = _levelCreator.LevelData.targetAreaHeight;

        // Resample: nearest-neighbor mapping from grid coords to texture coords
        for (int gy = 0; gy < gridH; gy++)
        {
            for (int gx = 0; gx < gridW; gx++)
            {
                int texX = Mathf.Clamp(Mathf.FloorToInt((float)gx / gridW * texWidth), 0, texWidth - 1);
                int texY = Mathf.Clamp(Mathf.FloorToInt((float)gy / gridH * texHeight), 0, texHeight - 1);

                // Texture Y=0 is bottom, grid Y=0 is top → flip for sampling
                int flippedTexY = (texHeight - 1) - texY;
                Color32 pixel = pixels[flippedTexY * texWidth + texX];

                // Skip transparent pixels
                if (pixel.a == 0)
                    continue;

                int colorId = _levelCreator.LevelData.GetOrAddColorId(pixel, _colorTolerance);
                _levelCreator.LevelData.targetDataList.Add(new TargetData(new Vector2Int(gx, gy), colorId));
            }
        }

        // Auto-calculate cell size to fit within conveyor bounds
        _levelCreator.LevelData.targetAreaSize = CalculateTargetAreaSize();

        _targetAreaGrid = GridHelper.CreateTargetAreaGrid(_levelCreator.LevelData, _mainConveyorBounds.center);

        _lastInitializedTargetWidth = gridW;
        _lastInitializedTargetHeight = gridH;
        _lastInitializedTargetSize = _levelCreator.LevelData.targetAreaSize;

        // Set brush to first color if palette not empty
        if (_levelCreator.LevelData.colorPalette.Count > 0)
        {
            _brushColorId = _levelCreator.LevelData.colorPalette[0].Id;
            EditorPrefs.SetInt(PrefKey_BrushColorId, _brushColorId);
        }

        MarkLevelDataDirty();
        CreateVisualsFromLevelData();
        UpdateBulletAndTargetsCounts();
    }

    #endregion

    #region AUTO SHOOTER GENERATION

    /// <summary>
    /// Layer-by-layer (outside-in) shooter generation.
    /// Each target's "layer" = min distance to any grid edge.
    /// Shooters are created per-layer per-color so that outer targets
    /// are cleared first, guaranteeing level completion.
    /// </summary>
    private void AutoGenerateShooters()
    {
        List<LevelColor> palette = _levelCreator.LevelData.colorPalette;
        if (palette == null || palette.Count == 0)
        {
            Debug.LogWarning("No color palette. Import a texture first.");
            return;
        }

        if (_levelCreator.LevelData.targetDataList == null || _levelCreator.LevelData.targetDataList.Count == 0)
        {
            Debug.LogWarning("No target data. Generate targets from texture first.");
            return;
        }

        RecordUndo("Auto Generate Shooters");

        int gridW = _levelCreator.LevelData.targetAreaWidth;
        int gridH = _levelCreator.LevelData.targetAreaHeight;

        // Group targets by (layer, colorId)
        SortedDictionary<int, Dictionary<int, int>> layerColorCounts = new SortedDictionary<int, Dictionary<int, int>>();

        foreach (var td in _levelCreator.LevelData.targetDataList)
        {
            int layer = Mathf.Min(Mathf.Min(td.Coordinates.x, td.Coordinates.y), Mathf.Min(gridW - 1 - td.Coordinates.x, gridH - 1 - td.Coordinates.y));

            if (!layerColorCounts.ContainsKey(layer))
                layerColorCounts[layer] = new Dictionary<int, int>();

            if (!layerColorCounts[layer].ContainsKey(td.ColorId))
                layerColorCounts[layer][td.ColorId] = 0;

            layerColorCounts[layer][td.ColorId]++;
        }

        // Create shooter specs in layer order with carry-forward.
        // Only the last layer per color uses greedy remainder;
        // all earlier layers carry leftover to the next layer
        // so that shooters always use counts from the configured list.
        var shooterSpecs = new List<(int colorId, int bulletCount)>();
        var carry = new Dictionary<int, int>();
        var layers = new List<int>(layerColorCounts.Keys);

        // Sort bullet counts descending for greedy allocation
        var sortedCounts = new List<int>(_shooterBulletCounts);
        sortedCounts.Sort((a, b) => b.CompareTo(a));

        for (int li = 0; li < layers.Count; li++)
        {
            int layer = layers[li];
            var colorCounts = layerColorCounts[layer];

            foreach (var colorKvp in colorCounts)
            {
                int colorId = colorKvp.Key;
                int layerCount = colorKvp.Value;
                carry.TryGetValue(colorId, out int carryCount);
                int accumulated = carryCount + layerCount;

                // Check if this color has targets in any future layer
                bool hasMoreTargets = false;
                for (int lj = li + 1; lj < layers.Count; lj++)
                {
                    if (layerColorCounts[layers[lj]].ContainsKey(colorId))
                    {
                        hasMoreTargets = true;
                        break;
                    }
                }

                if (!hasMoreTargets)
                {
                    // Last layer for this color → greedy with remainder
                    var counts = DecomposeIntoBulletCounts(accumulated, _shooterBulletCounts);
                    foreach (int c in counts)
                        shooterSpecs.Add((colorId, c));
                    carry[colorId] = 0;
                }
                else
                {
                    // Create only full shooters from the list, carry the rest
                    int remaining = accumulated;
                    while (remaining > 0)
                    {
                        bool found = false;
                        foreach (int count in sortedCounts)
                        {
                            if (count <= remaining)
                            {
                                shooterSpecs.Add((colorId, count));
                                remaining -= count;
                                found = true;
                                break;
                            }
                        }

                        if (!found) break;
                    }

                    carry[colorId] = remaining;
                }
            }
        }

        // Clear existing shooters
        _levelCreator.shooterParent.DestroyAllChildrenImmediate();
        _levelCreator.LevelData.shooterLaneDataList.Clear();

        // Initialize lanes
        int laneCount = _levelCreator.LevelData.shooterLaneCount;
        for (int i = 0; i < laneCount; i++)
            _levelCreator.LevelData.shooterLaneDataList.Add(new ShooterLaneData { ShooterDataList = new List<ShooterData>() });

        // Place shooters round-robin across lanes
        int[] laneY = new int[laneCount];
        int currentLane = 0;

        foreach (var (colorId, bulletCount) in shooterSpecs)
        {
            int x = currentLane;
            int y = laneY[x];
            int id = int.Parse((x + 1) + "" + y);

            var data = new ShooterData(id, bulletCount, colorId, -1, new Vector2Int(x, y), false);
            _levelCreator.LevelData.shooterLaneDataList[x].ShooterDataList.Add(data);

            laneY[x]++;
            currentLane = (currentLane + 1) % laneCount;
        }

        MarkLevelDataDirty();
        RecalculateShooterGridHeight();
        CreateVisualsFromLevelData();
        UpdateBulletAndTargetsCounts();
    }

    private List<int> DecomposeIntoBulletCounts(int total, List<int> availableCounts)
    {
        var result = new List<int>();
        if (total <= 0 || availableCounts == null || availableCounts.Count == 0)
            return result;

        // Sort descending for greedy allocation
        var sorted = new List<int>(availableCounts);
        sorted.Sort((a, b) => b.CompareTo(a));

        int remaining = total;

        while (remaining > 0)
        {
            bool found = false;
            foreach (int count in sorted)
            {
                if (count <= remaining)
                {
                    result.Add(count);
                    remaining -= count;
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                // Remainder is less than smallest available count — use as-is
                result.Add(remaining);
                remaining = 0;
            }
        }

        return result;
    }

    #endregion
}

#endif
