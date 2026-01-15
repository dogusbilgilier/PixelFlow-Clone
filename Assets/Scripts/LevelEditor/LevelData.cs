using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "LevelData", menuName = "Create New LevelData")]
public class LevelData : ScriptableObject
{
    public int laneCount = 3;
    public int storageCount = 5;
    public List<ShooterLaneData> shooterLaneDataList;
    public List<TargetData> targetDataList;
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