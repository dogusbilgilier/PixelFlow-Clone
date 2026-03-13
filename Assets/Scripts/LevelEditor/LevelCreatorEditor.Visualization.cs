#if UNITY_EDITOR
using Freya;
using Game;
using UnityEditor;
using UnityEngine;

public partial class LevelCreatorEditor
{
    #region BRUSH PREVIEW

    private bool TryGetTargetBrushPreviewBounds(out Bounds bounds, out Vector3[] rect)
    {
        bounds = default;
        rect = null;

        if (!_isMouseInTargetGrid)
            return false;

        int size = Mathf.Max(1, _targetBrushSize);
        Vector2Int center = _currentHoverCellCoords;

        int half = (size - 1) / 2;
        Vector2Int start = center - new Vector2Int(half, half);

        bool hasAny = false;
        float minX = 0f, maxX = 0f, minZ = 0f, maxZ = 0f;

        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                Vector2Int coords = start + new Vector2Int(x, y);

                if (!GridHelper.TryGetPositionFromCoords(_targetAreaGrid, coords, out var cellCenter))
                    continue;

                if (!hasAny)
                {
                    minX = maxX = cellCenter.x;
                    minZ = maxZ = cellCenter.z;
                    hasAny = true;
                }
                else
                {
                    minX = Mathf.Min(minX, cellCenter.x);
                    maxX = Mathf.Max(maxX, cellCenter.x);
                    minZ = Mathf.Min(minZ, cellCenter.z);
                    maxZ = Mathf.Max(maxZ, cellCenter.z);
                }
            }
        }

        if (!hasAny)
            return false;

        float cellSize = _targetAreaGrid.Size;
        float width = (maxX - minX) + cellSize;
        float depth = (maxZ - minZ) + cellSize;

        Vector3 centerPos = new Vector3((minX + maxX) * 0.5f, 0f, (minZ + maxZ) * 0.5f);
        bounds = new Bounds(centerPos, new Vector3(width, 0.01f, depth));

        float halfW = width * 0.5f;
        float halfD = depth * 0.5f;

        Vector3 topLeft = new Vector3(centerPos.x - halfW, 0f, centerPos.z + halfD);
        Vector3 topRight = new Vector3(centerPos.x + halfW, 0f, centerPos.z + halfD);
        Vector3 bottomRight = new Vector3(centerPos.x + halfW, 0f, centerPos.z - halfD);
        Vector3 bottomLeft = new Vector3(centerPos.x - halfW, 0f, centerPos.z - halfD);

        rect = new[] { topLeft, topRight, bottomRight, bottomLeft };
        return true;
    }

    private void DrawTargetBrushPreview()
    {
        if (!TryGetTargetBrushPreviewBounds(out Bounds bounds, out Vector3[] rect))
            return;

        const float y = 0.001f;
        for (int i = 0; i < rect.Length; i++)
            rect[i].y = y;

        Handles.DrawSolidRectangleWithOutline(rect, Color.white.WithAlpha(0.06f), Color.white.WithAlpha(0.9f));
        Handles.color = Color.white;
        Handles.DrawWireCube(bounds.center, new Vector3(bounds.size.x, 0.01f, bounds.size.z));
        Handles.DrawWireDisc(_hoverCellCenter, Vector3.up, _targetAreaGrid.Size * 0.3f);
    }

    #endregion

    #region HANDLES VISUALIZATION

    private void TryVisualizeGetMainConveyorsBounds()
    {
        if (!_drawConveyorBounds)
            return;
        Handles.color = Color.aquamarine;
        Handles.DrawWireCube(_mainConveyorBounds.center, _mainConveyorBounds.size);

        var pointSize = _mainConveyor.Spline.GetPointSize(0);
        Vector3 maxPoint = _mainConveyorBounds.max - new Vector3(pointSize, 0, pointSize);
        Vector3 minPoint = _mainConveyorBounds.min + new Vector3(pointSize, 0, pointSize);

        Bounds bounds = new Bounds(minPoint, Vector3.zero);
        bounds.Encapsulate(maxPoint);

        Handles.DrawWireCube(bounds.center, bounds.size);
    }

    private void VisualizeShooterGrid()
    {
        Handles.color = Color.aquamarine;
        DrawGridLines(_shooterAreaGrid);

        Handles.color = Color.darkKhaki;
        var positions = GridHelper.GetStoragePositions(_levelCreator.LevelData, _shooterAreaGrid);

        foreach (var position in positions)
            Handles.DrawWireCube(position, (Vector3.one * _shooterAreaGrid.Size).FlattenY());

        var bounds = GridHelper.GetGridBounds(_shooterAreaGrid);

        Handles.color = Color.blueViolet;
        Handles.DrawWireCube(bounds.center, bounds.size);
    }

    private void DrawGridLines(GameGrid grid)
    {
        float size = grid.Size;
        int width = grid.Width;
        int height = grid.Height;

        float startZ = grid.CenterPosition.z + (height * 0.5f * size);
        float startX = -((width - 1) * size * 0.5f);
        Vector3 firstCellCenter = new Vector3(startX, 0f, startZ);

        float half = size * 0.5f;

        Vector3 topLeft = firstCellCenter + new Vector3(-half, 0f, half);
        Vector3 topRight = firstCellCenter + new Vector3((width - 1) * size + half, 0f, half);
        Vector3 bottomLeft = firstCellCenter + new Vector3(-half, 0f, -((height - 1) * size + half));

        for (int x = 0; x <= width; x++)
        {
            Vector3 a = topLeft + Vector3.right * (x * size);
            Vector3 b = bottomLeft + Vector3.right * (x * size);
            Handles.DrawLine(a, b);
        }

        for (int y = 0; y <= height; y++)
        {
            Vector3 a = topLeft + Vector3.back * (y * size);
            Vector3 b = topRight + Vector3.back * (y * size);
            Handles.DrawLine(a, b);
        }
    }

    private void VisualizeTargetAreaGrid()
    {
        Handles.color = Color.aquamarine;
        DrawGridLines(_targetAreaGrid);

        var bounds = GridHelper.GetGridBounds(_targetAreaGrid);
        Handles.color = Color.blueViolet;
        Handles.DrawWireCube(bounds.center, bounds.size);
    }

    private void VisualizeShooterLinks()
    {
        var shooters = _levelCreator.shooterParent.transform.GetComponentsInChildren<Shooter>();
        foreach (var shooter in shooters)
        {
            if (shooter.Data.LinkedShooterID != -1)
            {
                if (TryGetShooterFromId(shooter.Data.LinkedShooterID, out Shooter linkedShooter))
                {
                    Handles.color = Color.white;
                    Handles.DrawLine(shooter.transform.position, linkedShooter.transform.position, 5);
                }
            }
        }
    }

    #region GAME AREA BOUND VISUALIZATION

    private Bounds _gameAreaBounds;
    private Camera _cam;

    private Camera MainCamera
    {
        get
        {
            if (_cam != null)
                return _cam;

            _cam = Camera.main;
            return _cam;
        }
    }

    private void TryVisualizeGameScene()
    {
        if (MainCamera == null || !_drawGameAreaBounds)
            return;

        float aspect = GetGameViewAspect(MainCamera);

        Plane plane = new Plane(Vector3.up, 0f);

        if (!TryGetViewportQuadOnPlane(MainCamera, aspect, plane, out Vector3[] quad))
            return;

        _gameAreaBounds = new Bounds(quad[0], Vector3.zero);
        for (int i = 1; i < quad.Length; i++)
            _gameAreaBounds.Encapsulate(quad[i]);

        Handles.color = Color.white.WithAlpha(0.5f);
        Handles.DrawWireCube(_gameAreaBounds.center, _gameAreaBounds.size);
        var style = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.UpperCenter };
        Handles.Label(_gameAreaBounds.max.z * Vector3.forward, "Visible Game Area", style);
    }

    private static float GetGameViewAspect(Camera cam)
    {
        if (Application.isPlaying && Screen.height > 0)
            return (float)Screen.width / Screen.height;

        Vector2 gv = Handles.GetMainGameViewSize();
        if (gv.y > 0.0001f)
            return gv.x / gv.y;

        return cam.aspect > 0f ? cam.aspect : (16f / 9f);
    }

    private static bool TryGetViewportQuadOnPlane(Camera cam, float desiredAspect, Plane plane, out Vector3[] quad)
    {
        quad = new Vector3[4];

        Vector2[] viewPorts = { new(0f, 0f), new(0f, 1f), new(1f, 1f), new(1f, 0f) };

        float prevAspect = cam.aspect;
        cam.aspect = desiredAspect;

        for (int i = 0; i < 4; i++)
        {
            Ray ray = cam.ViewportPointToRay(new Vector3(viewPorts[i].x, viewPorts[i].y, 0f));
            if (!plane.Raycast(ray, out float enter) || enter < 0f)
            {
                cam.aspect = prevAspect;
                return false;
            }

            quad[i] = ray.GetPoint(enter);
        }

        cam.aspect = prevAspect;
        return true;
    }

    #endregion

    private void OnHandlesDraw()
    {
        TryVisualizeGetMainConveyorsBounds();
        TryVisualizeGameScene();
        VisualizeShooterGrid();
        VisualizeTargetAreaGrid();
        VisualizeShooterLinks();
    }

    #endregion
}

#endif