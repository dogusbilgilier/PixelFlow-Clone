using System.Collections.Generic;
using Freya;
using UnityEngine;

public static class GridHelper
{
    public static bool TryGetPositionFromCoords(GameGrid grid, Vector2Int coords, out Vector3 cellCenter)
    {
        cellCenter = Vector3.zero;

        if (coords.x < 0 || coords.x >= grid.Width || coords.y < 0 || coords.y >= grid.Height)
            return false;

        float offsetX = (grid.Width - 1) * grid.Size * 0.5f;
        float startZ = grid.CenterPosition.z + (grid.Height * 0.5f * grid.Size);
        Vector3 startPos = new Vector3(-offsetX, 0f, startZ);

        cellCenter = startPos + (new Vector3(coords.x, 0f, -coords.y) * grid.Size);
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

        float centerZ = mainConveyorMinZ -
                        (height * size * 0.5f) -
                        (GameConfigs.Instance.shooterGridZOffsetToMainConveyorByGridSize * size);

        Vector3 centerPosition = Vector3.forward * centerZ;

        return new GameGrid(size, width, height, centerPosition);
    }

    public static Vector3[] GetStoragePositions(LevelData levelData, GameGrid shooterGrid)
    {
        Vector3[] storagePositions = new Vector3[levelData.storageCount];
        int storageCount = levelData.storageCount;
        float storageSize = levelData.shooterGridSize;
        float storageXPos = -((storageSize / 2f) * storageCount);

        float startZ = shooterGrid.CenterPosition.z + (shooterGrid.Height * 0.5f * shooterGrid.Size);
        Vector3 storageStartPos = new Vector3(storageXPos, 0f, startZ + (storageSize));

        for (int i = 0; i < storageCount; i++)
        {
            Vector3 position = storageStartPos + (Vector3.right * (storageSize * i)) + (Vector3.right * storageSize / 2f);
            storagePositions[i] = position;
        }

        return storagePositions;
    }

    public static Vector3[] GetGridPositions(GameGrid grid)
    {
        Vector3[] gridPositions = new Vector3[grid.Width * grid.Height];
        int index = 0;
        for (int x = 0; x < grid.Width; x++)
        {
            for (int y = 0; y < grid.Height; y++)
            {
                gridPositions[index] = new Vector3(x, 0f, -y) * grid.Size + grid.StartPosition;
                index++;
            }
        }

        return gridPositions;
    }

    public static GameGrid CreateTargetAreaGrid(LevelData levelData, Vector3 mainConveyorCenter)
    {
        int width = levelData.targetAreaWidth;
        float size = levelData.targetAreaSize;
        int height = levelData.targetAreaHeight;
        Vector3 centerPosition = mainConveyorCenter.FlattenY();
        return new GameGrid(size, width, height, centerPosition);
    }

    public static Bounds GetGridBounds(GameGrid grid)
    {
        Vector3 size = new Vector3(grid.Width * grid.Size, 0f, grid.Size * grid.Height);
        Bounds bounds = new Bounds(grid.CenterPosition + (Vector3.forward * (grid.Size * 0.5f)), size);
        return bounds;
    }
}