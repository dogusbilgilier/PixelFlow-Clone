using Sirenix.OdinInspector;
using UnityEngine;

namespace Game
{
    public class GridAndStorageVisualizer : MonoBehaviour
    {
        [Title("References")]
        [SerializeField] private GridPiece _gridPrefab;
        [SerializeField] private StoragePiece _storagePrefab;
        public StoragePiece[] StorageVisualPieces { get; private set; }

        public bool IsInitialized { get; private set; }
        public bool IsPrepared { get; private set; }
        public bool enableShooterGridPieces;

        public void Initialize(GameGrid shooterGrid)
        {
            IsInitialized = true;
        }

        public void Prepare(GameGrid shooterGrid)
        {
            GeneratePieces(shooterGrid);
            IsPrepared = true;
        }

        private void GeneratePieces(GameGrid shooterGrid)
        {
            if (StorageVisualPieces != null && StorageVisualPieces.Length > 0)
            {
                for (int i = StorageVisualPieces.Length - 1; i >= 0; i--)
                    DestroyImmediate(StorageVisualPieces[i]);
            }

            float sizeMultiplier = 0.7f;
            var storagePositions = GridHelper.GetStoragePositions(LevelManager.Instance.CurrentLevelData, shooterGrid);
            StorageVisualPieces = new StoragePiece[storagePositions.Length];

            for (var i = 0; i < storagePositions.Length; i++)
            {
                var storagePosition = storagePositions[i];
                var storagePiece = Instantiate(_storagePrefab, transform);
                storagePiece.transform.position = storagePosition;
                storagePiece.transform.localScale =Vector3.one;
                StorageVisualPieces[i] = storagePiece;
            }

            if (enableShooterGridPieces)
            {
                var gridPositions = GridHelper.GetGridPositions(shooterGrid);
                foreach (var gridPosition in gridPositions)
                {
                    var gridPiece = Instantiate(_gridPrefab, transform);
                    gridPiece.transform.position = gridPosition;
                    gridPiece.transform.localScale = new Vector3(shooterGrid.Size * 0.75f, 0.1f, shooterGrid.Size * 0.75f) * sizeMultiplier;
                }
            }
        }
    }
}