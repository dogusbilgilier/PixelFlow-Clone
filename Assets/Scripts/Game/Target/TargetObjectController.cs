using System;
using System.Collections.Generic;
using Freya;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Pool;

namespace Game
{
    public class TargetObjectController : MonoBehaviour
    {
        [Title("References")]
        [SerializeField] private TargetObject _targetObjectPrefab;
        [SerializeField] private Transform _targetObjectParent;

        private TargetObject[][] _targetObjectJaggedArray;
        private readonly List<TargetObject> _foundedTargets = new List<TargetObject>();

        private Bounds _mainConveyorBounds;
        private GameGrid _targetAreaGrid;
        private Bounds _targetAreaBounds;

        private int _totalTargetCount;
        private int _destroyedTargetCount;
        public bool IsInitialized { get; private set; }
        public bool IsPrepared { get; private set; }
        public event Action OnAllTargetsDestroyed;

        public void Initialize(Bounds mainConveyorBounds)
        {
            _mainConveyorBounds = mainConveyorBounds;
            IsInitialized = true;
        }

        public void Prepare()
        {
            IsPrepared = false;

            _totalTargetCount = 0;
            _targetAreaGrid = GridHelper.CreateTargetAreaGrid(LevelManager.Instance.CurrentLevelData, _mainConveyorBounds.center);
            _targetAreaBounds = GridHelper.GetGridBounds(_targetAreaGrid);

            CreateTargetObjects();

            IsPrepared = true;
        }

        private void OnDestroy()
        {
            OnAllTargetsDestroyed = null;
        }

        private void CreateTargetObjects()
        {
            _targetObjectParent.DestroyAllChildrenImmediate();
            _targetObjectJaggedArray = new TargetObject[LevelManager.Instance.CurrentLevelData.targetAreaWidth][];

            for (int i = 0; i < _targetObjectJaggedArray.Length; i++)
                _targetObjectJaggedArray[i] = new TargetObject[LevelManager.Instance.CurrentLevelData.targetAreaHeight];

            foreach (TargetData targetData in LevelManager.Instance.CurrentLevelData.targetDataList)
            {
                if (GridHelper.TryGetPositionFromCoords(_targetAreaGrid, targetData.Coordinates, out Vector3 position))
                {
                    var targetObject = Instantiate(_targetObjectPrefab, _targetObjectParent);
                    position.y = _targetObjectParent.position.y;
                    targetObject.transform.position = position;
                    targetObject.transform.localScale = LevelManager.Instance.CurrentLevelData.targetAreaSize * Vector3.one;
                    targetObject.Initialize(targetData);
                    targetObject.OnTargetHit += TargetObject_OnTargetHit;
                    _totalTargetCount++;
                    _targetObjectJaggedArray[targetData.Coordinates.x][targetData.Coordinates.y] = targetObject;
                }
                else
                {
                    Debug.LogError("Target coordinates are not in grid bounds");
                }
            }
        }

        private void TargetObject_OnTargetHit(TargetObject targetObject)
        {
            _destroyedTargetCount++;
            if (_destroyedTargetCount >= _totalTargetCount)
            {
                OnAllTargetsDestroyed?.Invoke();
            }
        }

        public bool TryFindTargetForShooter(Shooter shooter, out List<TargetObject> targets, out Side side)
        {
            _foundedTargets.Clear();
            targets = _foundedTargets;
            side = Side.Bottom;

            if (shooter is null)
                return false;

            var shooterPos = shooter.transform.position;

            bool isBetweenX = shooterPos.x <= _targetAreaBounds.max.x && shooterPos.x >= _targetAreaBounds.min.x;
            bool isBetweenZ = shooterPos.z <= _targetAreaBounds.max.z && shooterPos.z >= _targetAreaBounds.min.z;

            if (shooterPos.z < _targetAreaBounds.min.z && isBetweenX)
                side = Side.Bottom;
            else if (shooterPos.z > _targetAreaBounds.max.z && isBetweenX)
                side = Side.Top;
            else if (shooterPos.x > _targetAreaBounds.max.x && isBetweenZ)
                side = Side.Right;
            else if (shooterPos.x < _targetAreaBounds.min.x && isBetweenZ)
                side = Side.Left;
            else
                return false;

            CheckForMissingPositions(shooter, out int stepCount);

            Vector3 currentPos = shooter.transform.position;
            Vector3 lastPos = shooter.ShooterTargetData.LastCheckPosition ?? currentPos;

            stepCount = Mathf.Max(1, stepCount);

            bool isTargetFound = false;
            for (int i = 1; i <= stepCount; i++)
            {
                float t = (float)i / stepCount;
                Vector3 scanPosition = Vector3.Lerp(lastPos, currentPos, t);

                if (TryFindTargetObject(scanPosition, side, out var targetObject))
                {
                    isTargetFound = true;
                    targets.Add(targetObject);
                }
            }

            shooter.ShooterTargetData.UpdateCheckPosition(currentPos);

            return isTargetFound;
        }

        private void CheckForMissingPositions(Shooter shooter, out int stepCount)
        {
            Vector3? lastCheckPos = shooter.ShooterTargetData.LastCheckPosition;
            Vector3 currentPos = shooter.transform.position;
            stepCount = 1;

            if (!lastCheckPos.HasValue)
                return;

            float distance = Vector3.Distance(lastCheckPos.Value, currentPos);
            float targetObjectSize = LevelManager.Instance.CurrentLevelData.targetAreaSize;

            if (distance > targetObjectSize)
                stepCount = Mathf.CeilToInt(distance / targetObjectSize);
        }

        private bool TryFindTargetObject(Vector3 shooterPos, Side side, out TargetObject targetObject)
        {
            targetObject = null;

            if (!TryGetShooterGridCoords(shooterPos, side, out Vector2Int coords))
                return false;

            int x;
            int y;
            int dx;
            int dy;
            int steps;

            if (side == Side.Bottom)
            {
                x = coords.x;

                if (!IsValidOuterIndex(x) || _targetObjectJaggedArray[x] == null)
                    return false;

                y = _targetObjectJaggedArray[x].Length - 1;
                dx = 0;
                dy = -1;
                steps = _targetObjectJaggedArray[x].Length;
            }
            else if (side == Side.Top)
            {
                x = coords.x;

                if (!IsValidOuterIndex(x) || _targetObjectJaggedArray[x] == null)
                    return false;

                y = 0;
                dx = 0;
                dy = 1;
                steps = _targetObjectJaggedArray[x].Length;
            }
            else if (side == Side.Right)
            {
                y = coords.y;
                x = _targetObjectJaggedArray.Length - 1;
                dx = -1;
                dy = 0;
                steps = _targetObjectJaggedArray.Length;
            }
            else if (side == Side.Left)
            {
                y = coords.y;
                x = 0;
                dx = 1;
                dy = 0;
                steps = _targetObjectJaggedArray.Length;
            }
            else
                return false;

            for (int i = 0; i < steps; i++)
            {
                if (TryGetAliveTargetAt(x, y, out targetObject))
                    return true;

                x += dx;
                y += dy;
            }

            return false;
        }

        private bool TryGetShooterGridCoords(Vector3 shooterPos, Side side, out Vector2Int coords)
        {
            coords = default;
            Vector3 shooterTargetGridPos;

            if (side is Side.Bottom or Side.Top)
                shooterTargetGridPos = new Vector3(shooterPos.x, 0f, _targetAreaGrid.CenterPosition.z);
            else
                shooterTargetGridPos = new Vector3(_targetAreaGrid.CenterPosition.x, 0f, shooterPos.z);

            return GridHelper.TryGetGridFromPosition(_targetAreaGrid, shooterTargetGridPos, out coords, out _);
        }

        private bool TryGetAliveTargetAt(int x, int y, out TargetObject targetObject)
        {
            targetObject = null;

            if (!IsValidOuterIndex(x))
                return false;

            TargetObject[] column = _targetObjectJaggedArray[x];

            if (column == null || y < 0 || y >= column.Length)
                return false;

            TargetObject candidate = column[y];

            if (candidate == null || candidate.IsDestroyed)
                return false;

            targetObject = candidate;
            return true;
        }

        private bool IsValidOuterIndex(int x)
        {
            return x >= 0 && x < _targetObjectJaggedArray.Length;
        }
    }
}