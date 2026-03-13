using System;
using UnityEngine;

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