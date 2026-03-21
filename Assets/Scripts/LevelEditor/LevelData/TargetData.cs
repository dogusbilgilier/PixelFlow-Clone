using System;
using UnityEngine;

namespace Game
{
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
}