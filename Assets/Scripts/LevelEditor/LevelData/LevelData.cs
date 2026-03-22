using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;

namespace Game
{
[CreateAssetMenu(fileName = "LevelData", menuName = "Create New LevelData")]
public class LevelData : ScriptableObject
{
    [Title("Color Palette")]
    public Texture2D sourceTexture;
    public bool useColorGrouping;
    public List<LevelColor> colorPalette = new();

    [Title("Target Area")]
    public int targetAreaWidth = 20;
    public int targetAreaHeight = 20;
    public float targetAreaSize = 0.5f;
    public List<TargetData> targetDataList;

    [Title("Shooter Area")]
    public int shooterLaneCount = 3;
    public int shooterLaneHeight = 40;
    public float shooterGridSize = 1f;
    public int storageCount = 5;
    public List<ShooterLaneData> shooterLaneDataList;

    [Title("Conveyor")]
    public int conveyorBoardCount = 5;

    public Color32 GetColorById(int colorId)
    {
        if (colorPalette != null)
        {
            foreach (var lc in colorPalette)
            {
                if (lc.Id == colorId)
                    return lc.Color;
            }
        }

        return new Color32(255, 255, 255, 255);
    }

    public int GetOrAddColorId(Color32 color, float tolerance = 0f)
    {
        if (colorPalette == null)
            colorPalette = new List<LevelColor>();

        foreach (var lc in colorPalette)
        {
            if (tolerance <= 0f)
            {
                if (lc.Color.r == color.r && lc.Color.g == color.g && lc.Color.b == color.b && lc.Color.a == color.a)
                    return lc.Id;
            }
            else
            {
                float dist = Mathf.Sqrt(
                    (lc.Color.r - color.r) * (lc.Color.r - color.r) +
                    (lc.Color.g - color.g) * (lc.Color.g - color.g) +
                    (lc.Color.b - color.b) * (lc.Color.b - color.b));
                if (dist <= tolerance)
                    return lc.Id;
            }
        }

        int newId = colorPalette.Count > 0 ? colorPalette[colorPalette.Count - 1].Id + 1 : 0;
        colorPalette.Add(new LevelColor { Id = newId, Color = color, ShooterColorId = newId });
        return newId;
    }

    /// <summary>
    /// Adds the exact color as a unique palette entry but groups similar colors
    /// under the same ShooterColorId for shooter generation/matching.
    /// </summary>
    public int GetOrAddColorIdGrouped(Color32 color, float tolerance)
    {
        if (colorPalette == null)
            colorPalette = new List<LevelColor>();

        // Exact match → return existing
        foreach (var lc in colorPalette)
        {
            if (lc.Color.r == color.r && lc.Color.g == color.g && lc.Color.b == color.b && lc.Color.a == color.a)
                return lc.Id;
        }

        // Find a similar color to share ShooterColorId with
        int groupId = -1;
        if (tolerance > 0f)
        {
            foreach (var lc in colorPalette)
            {
                float dist = Mathf.Sqrt(
                    (lc.Color.r - color.r) * (lc.Color.r - color.r) +
                    (lc.Color.g - color.g) * (lc.Color.g - color.g) +
                    (lc.Color.b - color.b) * (lc.Color.b - color.b));
                if (dist <= tolerance)
                {
                    groupId = lc.ShooterColorId;
                    break;
                }
            }
        }

        int newId = colorPalette.Count > 0 ? colorPalette[colorPalette.Count - 1].Id + 1 : 0;
        colorPalette.Add(new LevelColor
        {
            Id = newId,
            Color = color,
            ShooterColorId = groupId >= 0 ? groupId : newId
        });
        return newId;
    }

    /// <summary>
    /// Returns the ShooterColorId for a given color ID.
    /// When useColorGrouping is false, returns the colorId itself.
    /// </summary>
    public int GetShooterColorId(int colorId)
    {
        if (!useColorGrouping)
            return colorId;

        foreach (var lc in colorPalette)
        {
            if (lc.Id == colorId)
                return lc.ShooterColorId;
        }

        return colorId;
    }
}
}