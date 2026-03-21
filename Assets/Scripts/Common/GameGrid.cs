using UnityEngine;

namespace Game
{
    public class GameGrid
    {
        public float Size;
        public int Width;
        public int Height;
        public Vector3 CenterPosition;
        public Vector3 StartPosition;

        public GameGrid(float size, int width, int height, Vector3 centerPosition)
        {
            Size = size;
            Width = width;
            Height = height;
            CenterPosition = centerPosition;
            float startX = -((Width - 1) * Size * 0.5f);
            float startZ = CenterPosition.z + (Height * 0.5f * Size);
            StartPosition = new Vector3(startX, 0f, startZ);
        }
    }
}