using Freya;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game
{
    public class TargetObjectController : MonoBehaviour
    {
        [Title("References")]
        [SerializeField] private TargetObject _targetObjectPrefab;

        [SerializeField] private Transform _targetObjectParent;

        private Bounds _mainConveyorBounds;
        private GameGrid _targetAreaGrid;
        public bool IsInitialized { get; private set; }

        public void Initialize(Bounds mainConveyorBounds)
        {
            _mainConveyorBounds = mainConveyorBounds;
            CreateTargetGrid();
            CreateTargetObjects();
            IsInitialized = true;
        }

        private void CreateTargetGrid()
        {
            float size = LevelManager.Instance.CurrentLevelData.targetAreaSize;
            int width = LevelManager.Instance.CurrentLevelData.targetAreaWidth;
            int height = LevelManager.Instance.CurrentLevelData.targetAreaHeight;
            Vector3 centerPosition = _mainConveyorBounds.center.FlattenY();
            _targetAreaGrid = new GameGrid(size, width, height, centerPosition);
        }

        private void CreateTargetObjects()
        {
            foreach (TargetData targetData in LevelManager.Instance.CurrentLevelData.targetDataList)
            {
                if (GridHelper.TryGetPositionFromCoords(_targetAreaGrid, targetData.Coordinates, out Vector3 position))
                {
                    var targetObject = Instantiate(_targetObjectPrefab, _targetObjectParent);
                    targetObject.transform.position = position;
                    targetObject.transform.localScale = LevelManager.Instance.CurrentLevelData.targetAreaSize * Vector3.one;
                    targetObject.Initialize(targetData);
                }
                else
                {
                    Debug.LogError("Target coordinates are not in grid bounds");
                }
            }
        }
    }
}