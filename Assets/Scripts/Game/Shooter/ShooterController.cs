using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Pool;

namespace Game
{
    public class ShooterController : MonoBehaviour
    {
        [Title("References")]
        [SerializeField] private Shooter _shooterPrefab;
        [SerializeField] private Transform _shooterParent;
        public bool IsInitialized { get; private set; }

        public GameGrid ShooterGrid => _shooterAreaGrid;
        //BULLET
        private ObjectPool<Bullet> _bulletPool;
        [SerializeField] private Bullet _bulletPrefab;
        [SerializeField] private Transform _bulletParent;
        //-----
        private ShooterLaneController _shooterLaneController;
        private GameGrid _shooterAreaGrid;
        private Bounds _mainConveyorBounds;
        private List<Shooter> _allShooters = new List<Shooter>();
        private List<Shooter> _currentlyMovingShooters = new List<Shooter>();
        public List<Shooter> CurrentlyMovingShooters => _currentlyMovingShooters;

        public event Action<Shooter> OnShooterJumpRequest;
        public event Action<Shooter> OnShooterCompletedPath;
        public event Action<Shooter> OnShooterDestroyed;

        public void Initialize(Bounds mainConveyorBounds)
        {
            _mainConveyorBounds = mainConveyorBounds;
            _shooterAreaGrid = GridHelper.CreateShooterGrid(LevelManager.Instance.CurrentLevelData, _mainConveyorBounds.min.z);

            CreateAllShooters();
            _shooterLaneController = new ShooterLaneController(_allShooters, _shooterAreaGrid);
            _bulletPool = new ObjectPool<Bullet>(OnCreateBullet, OnGetBullet, OnReleaseBullet, OnDestroyBullet, defaultCapacity: 20);

            IsInitialized = true;
        }


        private void OnDestroy()
        {
            OnShooterJumpRequest = null;
            OnShooterCompletedPath = null;
            OnShooterDestroyed = null;
        }

        private void CreateAllShooters()
        {
            for (int i = 0; i < LevelManager.Instance.CurrentLevelData.shooterLaneCount; i++)
            {
                ShooterLaneData laneData = LevelManager.Instance.CurrentLevelData.shooterLaneDataList[i];

                foreach (var shooterData in laneData.ShooterDataList)
                    _allShooters.Add(CreateShooter(shooterData));
            }
        }

        private Shooter CreateShooter(ShooterData shooterData)
        {
            if (GridHelper.TryGetPositionFromCoords(_shooterAreaGrid, shooterData.Coordinates, out Vector3 position))
            {
                Shooter shooter = Instantiate(_shooterPrefab, _shooterParent);
                shooter.transform.position = position;
                shooter.Initialize(shooterData);
                shooter.OnJumpRequest += ShooterOnOnJumpRequest;
                shooter.OnCompletedPath += Shooter_OnCompletedPath;
                shooter.OnBulletsExhausted += ShooterOnBulletsExhausted;
                return shooter;
            }

            Debug.LogError("Shooter coordinates is out of grid!");
            return null;
        }

        private void ShooterOnBulletsExhausted(Shooter shooter)
        {
            OnShooterDestroyed?.Invoke(shooter);
        }

        private bool CheckCanShooterJump()
        {
            //TODO
            return true;
        }

        private void ShooterOnOnJumpRequest(Shooter shooter)
        {
            if (!CheckCanShooterJump())
                return;

            OnShooterJumpRequest?.Invoke(shooter);
        }

        private void Shooter_OnCompletedPath(Shooter shooter)
        {
            OnShooterCompletedPath?.Invoke(shooter);
        }

        public void AddMovingShooter(Shooter shooter)
        {
            _currentlyMovingShooters.Add(shooter);
        }

        public void RemoveMovingShooter(Shooter shooter)
        {
            _currentlyMovingShooters.Remove(shooter);
        }

        public void ShooterJumpToConveyorFromLane(Shooter shooter)
        {
            _shooterLaneController.ShooterJumpToConveyorFromLane(shooter);
        }

        public bool TryShootForTarget(Shooter shooter, TargetObject targetObject, Side side)
        {
            if (targetObject == null)
                return false;

            if (shooter.IsBulletsExhausted)
                return false;

            if (shooter.Data.Color != targetObject.Data.Color)
                return false;

            if (!shooter.ShooterTargetData.CheckForData(side, targetObject.Data.Coordinates))
                return false;

            var bullet = _bulletPool.Get();
            shooter.OnShoot(targetObject, side, bullet);


            return true;
        }

        #region BULLET POOL

        private void OnDestroyBullet(Bullet bullet)
        {
            Destroy(bullet.gameObject);
        }

        private void OnReleaseBullet(Bullet bullet)
        {
            bullet.gameObject.SetActive(false);
        }

        private void OnGetBullet(Bullet bullet)
        {
            bullet.gameObject.SetActive(true);
        }

        private Bullet OnCreateBullet()
        {
            var bullet = Instantiate(_bulletPrefab, _bulletParent);
            bullet.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            bullet.AssignPool(_bulletPool);
            return bullet;
        }

        #endregion
    }
}