using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "LevelData", menuName = "Create New LevelData")]
public class LevelData : ScriptableObject
{
    [Title("Color Palette")]
    public Texture2D sourceTexture;
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
        colorPalette.Add(new LevelColor { Id = newId, Color = color });
        return newId;
    }
}

[Serializable]
public class LevelColor
{
    public int Id;
    public Color32 Color;
}

[Serializable]
public class ShooterLaneData
{
    public List<ShooterData> ShooterDataList;
}

[Serializable]
public class ShooterData
{
    public int ID;
    public int BulletCount;
    public int ColorId;
    public int LinkedShooterID;
    public Vector2Int Coordinates;
    public bool IsHidden;

    public ShooterData(int id, int bulletCount, int colorId, int linkedShooterId, Vector2Int coordinates, bool isHidden)
    {
        ID = id;
        BulletCount = bulletCount;
        ColorId = colorId;
        LinkedShooterID = linkedShooterId;
        Coordinates = coordinates;
        IsHidden = isHidden;
    }
}

[Serializable]
public class TargetData
{
    public Vector2Int Coordinates;
    public int ColorId;

    public TargetData(Vector2Int coordinates, int colorId)
    {
        Coordinates = coordinates;
        ColorId = colorId;
    }
}
