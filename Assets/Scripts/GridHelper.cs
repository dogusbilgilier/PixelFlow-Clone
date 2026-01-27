using System.Collections.Generic;
using Freya;
using UnityEngine;

public static class GridHelper
{
    public static List<Vector3> GetGridCenters(GameGrid grid)
    {
        List<Vector3> gridCenters = new List<Vector3>();

        float size = grid.Size;
        int width = grid.Width;
        int height = grid.Height;
        float startZ = grid.CenterPosition.z + (height * 0.5f * size);
        float startX = -((width - 1) * size * 0.5f);
        Vector3 startPos = new Vector3(startX, 0, startZ);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector3 pos = new Vector3(x, 0, -y) * size + startPos;
                gridCenters.Add(pos);
            }
        }

        return gridCenters;
    }

    public static bool TryGetPositionFromCoords(GameGrid grid, Vector2Int coords, out Vector3 cellPosition)
    {
        cellPosition = Vector3.zero;

        if (coords.x < 0 || coords.x >= grid.Width) return false;
        if (coords.y < 0 || coords.y >= grid.Height) return false;

        float offsetX = (grid.Width - 1) * grid.Size * 0.5f;
        float startZ = grid.CenterPosition.z + (grid.Height * 0.5f * grid.Size);
        Vector3 startPos = new Vector3(-offsetX, 0f, startZ);

        cellPosition = startPos + new Vector3(coords.x, 0f, -coords.y) * grid.Size;
        return true;
    }

    public static bool TryGetGridFromPosition(GameGrid grid, Vector3 position, out Vector2Int coords, out Vector3 cellCenter)
    {
        float size = grid.Size;
        int width = grid.Width;
        int height = grid.Height;

        coords = Vector2Int.zero;
        cellCenter = Vector3.zero;

        float half = size * 0.5f;
        float offsetX = (width - 1) * size * 0.5f;
        float startZ = grid.CenterPosition.z + (height * 0.5f * size);
        Vector3 startPos = new Vector3(-offsetX, 0f, startZ);

        position.y = 0f;

        int x = Mathf.FloorToInt(((position.x - startPos.x) + half) / size);
        int y = Mathf.FloorToInt(((-(position.z - startPos.z)) + half) / size);

        if (x < 0 || x >= width) return false;
        if (y < 0 || y >= height) return false;

        cellCenter = startPos + new Vector3(x, 0f, -y) * size;
        coords = new Vector2Int(x, y);
        return true;
    }

    public static GameGrid CreateShooterGrid(LevelData levelData, float mainConveyorMinZ)
    {
        int width = levelData.shooterLaneCount;
        float size = levelData.shooterGridSize;
        int height = levelData.shooterLaneHeight;

        float centerZ = mainConveyorMinZ-
                        ((height + 1) * size * 0.5f) -
                        (GameConfigs.Instance.gridZOffsetToMainConveyorByGrid * size);

        Vector3 position = Vector3.forward * centerZ;
        return new GameGrid(size, width, height, position);
    }

    public static GameGrid CreateTargetAreaGrid(LevelData levelData, Vector3 mainConveyorCenter)
    {
        int width = levelData.targetAreaWidth;
        float size = levelData.targetAreaSize;
        int height = levelData.targetAreaHeight;
        Vector3 centerPosition = mainConveyorCenter.FlattenY();
        return new GameGrid(size, width, height, centerPosition);
    }
}