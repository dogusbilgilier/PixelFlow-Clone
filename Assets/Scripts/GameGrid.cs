using UnityEngine;

public class GameGrid
{
    public float Size;
    public int Width;
    public int Height;
    public Vector3 CenterPosition;

    public GameGrid(float size, int width, int height, Vector3 centerPosition)
    {
        Size = size;
        Width = width;
        Height = height;
        CenterPosition = centerPosition;
    }
}