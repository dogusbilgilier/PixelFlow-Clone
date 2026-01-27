using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "LevelData", menuName = "Create New LevelData")]
public class LevelData : ScriptableObject
{
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
    public GameColor Color;
    public int LinkedShooterID;
    public Vector2Int Coordinates;
    public bool IsHidden;

    public ShooterData(int id, int bulletCount, GameColor color, int linkedShooterId, Vector2Int coordinates, bool isHidden)
    {
        ID = id;
        BulletCount = bulletCount;
        Color = color;
        LinkedShooterID = linkedShooterId;
        Coordinates = coordinates;
        IsHidden = isHidden;
    }
}

[Serializable]
public class TargetData
{
    public Vector2Int Coordinates;
    public GameColor Color;

    public TargetData(Vector2Int coordinates, GameColor color)
    {
        Coordinates = coordinates;
        Color = color;
    }
}