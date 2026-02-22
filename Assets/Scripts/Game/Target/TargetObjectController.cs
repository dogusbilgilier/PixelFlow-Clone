using System.Collections.Generic;
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

        private TargetObject[][] _targetObjectJaggedArray;

        private Bounds _mainConveyorBounds;
        private GameGrid _targetAreaGrid;
        private Bounds _targetAreaBounds;
        public bool IsInitialized { get; private set; }

        public void Initialize(Bounds mainConveyorBounds)
        {
            _mainConveyorBounds = mainConveyorBounds;
            _targetAreaGrid = GridHelper.CreateTargetAreaGrid(LevelManager.Instance.CurrentLevelData, _mainConveyorBounds.center);
            _targetAreaBounds = GridHelper.GetGridBounds(_targetAreaGrid);

            CreateTargetObjects();

            IsInitialized = true;
        }

        private void CreateTargetObjects()
        {
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
                    _targetObjectJaggedArray[targetData.Coordinates.x][targetData.Coordinates.y] = targetObject;
                }
                else
                {
                    Debug.LogError("Target coordinates are not in grid bounds");
                }
            }
        }

        public void CheckForShooter(Shooter shooter)
        {
            var shooterPos = shooter.transform.position;
            bool isBetweenX = shooterPos.x <= _targetAreaBounds.max.x && shooterPos.x >= _targetAreaBounds.min.x;
            bool isBetweenZ = shooterPos.z <= _targetAreaBounds.max.z && shooterPos.z >= _targetAreaBounds.min.z;

            Side? side = null;
            if (shooterPos.z < _targetAreaBounds.min.z && isBetweenX) //BOTTOM
                side = Side.Bottom;
            else if (shooter.transform.position.z > _targetAreaBounds.max.z && isBetweenX) //TOP
                side = Side.Top;
            else if (shooter.transform.position.x > _targetAreaBounds.max.x && isBetweenZ) //RIGHT
                side = Side.Right;
            else if (shooter.transform.position.x < _targetAreaBounds.min.x && isBetweenZ) //LEFT
                side = Side.Left;

            if (side == null)
                return;

            if (TryFindTargetObject(shooterPos, side.Value, out var targetObject))
                ShootToTarget(targetObject, shooter, side.Value);
        }


        private bool TryFindTargetObject(Vector3 shooterPos, Side side, out TargetObject targetObject)
        {
            targetObject = null;

            if (!TryGetShooterGridCoords(shooterPos, side, out Vector2Int coords))
                return false;

            int x = 0;
            int y = 0;
            int dx = 0;
            int dy = 0;
            int steps = 0;

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
            {
                return false;
            }

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

            Vector3 shooterCheckPos;

            switch (side)
            {
                case Side.Bottom:
                case Side.Top:
                    shooterCheckPos = new Vector3(shooterPos.x, 0f, _targetAreaGrid.CenterPosition.z);
                    break;

                case Side.Left:
                case Side.Right:
                    shooterCheckPos = new Vector3(_targetAreaGrid.CenterPosition.x, 0f, shooterPos.z);
                    break;

                default:
                    return false;
            }

            return GridHelper.TryGetGridFromPosition(_targetAreaGrid, shooterCheckPos, out coords, out _);
        }

        private bool TryGetAliveTargetAt(int x, int y, out TargetObject targetObject)
        {
            targetObject = null;

            if (!IsValidOuterIndex(x))
                return false;

            var column = _targetObjectJaggedArray[x];
            
            if (column == null || y < 0 || y >= column.Length)
                return false;

            var candidate = column[y];
            
            if (candidate == null || candidate.IsDestroyed)
                return false;

            targetObject = candidate;
            return true;
        }

        private bool IsValidOuterIndex(int x)
        {
            return x >= 0 && x < _targetObjectJaggedArray.Length;
        }

        private void ShootToTarget(TargetObject targetObject, Shooter shooter, Side side)
        {
            if (targetObject == null)
                return;

            if (shooter.Data.Color != targetObject.Data.Color)
                return;

            if (!shooter.ShooterTargetData.CheckForData(side, targetObject.Data.Coordinates))
                return;

            shooter.OnShootToTarget(targetObject,side);
            targetObject.OnHit();
        }
    }
}