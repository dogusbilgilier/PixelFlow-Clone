using System;
using UnityEngine;

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