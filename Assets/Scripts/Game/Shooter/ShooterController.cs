using System;
using System.Collections.Generic;
using DG.Tweening;
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
        [SerializeField] private LinkObject _linkObjectPrefab;
        public bool IsInitialized { get; private set; }
        public bool IsPrepared { get; private set; }

        public List<Shooter> CurrentlyMovingShooters => _currentlyMovingShooters;
        public GameGrid ShooterGrid => _shooterAreaGrid;

        //BULLET
        private ObjectPool<Bullet> _bulletPool;
        [SerializeField] private Bullet _bulletPrefab;
        [SerializeField] private Transform _bulletParent;
        //-----
        private ShooterLaneController _shooterLaneController;
        private GameGrid _shooterAreaGrid;
        private Bounds _mainConveyorBounds;
        private readonly List<Shooter> _allShooters = new List<Shooter>();
        private readonly List<Shooter> _currentlyMovingShooters = new List<Shooter>();
        private readonly List<LinkObject> _linkObjects = new List<LinkObject>();
        private readonly List<Bullet> _bullets = new List<Bullet>();

        public event Action<Shooter, bool /*skip interval*/> OnShooterJumpRequest;
        public event Action<Shooter> OnShooterCompletedPath;
        public event Action<Shooter> OnShooterDestroyed;
        public event Action OnAllShootersCompleted;


        public void Initialize(Bounds mainConveyorBounds)
        {
            _mainConveyorBounds = mainConveyorBounds;
            _bulletPool = new ObjectPool<Bullet>(OnCreateBullet, OnGetBullet, OnReleaseBullet, OnDestroyBullet, defaultCapacity: 30);
            IsInitialized = true;
        }

        public void Prepare()
        {
            IsPrepared = false;

            _shooterAreaGrid = GridHelper.CreateShooterGrid(LevelManager.Instance.CurrentLevelData, _mainConveyorBounds.min.z);
            UnloadShooters();
            CreateAllShooters();
            _shooterLaneController?.Dispose();
            _shooterLaneController = new ShooterLaneController(_allShooters, _shooterAreaGrid, this);
            _shooterLaneController.Initialize();
            _shooterLaneController.OnAllLanesCompleted += ShooterLaneController_OnAllLanesCompleted;

            IsPrepared = true;
        }

        private void UnloadShooters()
        {
            foreach (Shooter shooter in _allShooters)
                DestroyImmediate(shooter.gameObject);

            _allShooters.Clear();
            _currentlyMovingShooters.Clear();

            foreach (LinkObject link in _linkObjects)
            {
                link.BreakLink();
                DestroyImmediate(link.gameObject);
            }

            _linkObjects.Clear();

            foreach (Bullet bullet in _bullets)
                bullet.ForceRelease();
        }

        private void ShooterLaneController_OnAllLanesCompleted()
        {
            OnAllShootersCompleted?.Invoke();
        }

        private void OnDestroy()
        {
            OnShooterJumpRequest = null;
            OnShooterCompletedPath = null;
            OnShooterDestroyed = null;
            OnAllShootersCompleted = null;
        }

        private void CreateAllShooters()
        {
            for (int i = 0; i < LevelManager.Instance.CurrentLevelData.shooterLaneCount; i++)
            {
                ShooterLaneData laneData = LevelManager.Instance.CurrentLevelData.shooterLaneDataList[i];

                foreach (var shooterData in laneData.ShooterDataList)
                    _allShooters.Add(CreateShooter(shooterData));
            }

            foreach (Shooter shooter1 in _allShooters)
            {
                if (shooter1.Data.LinkedShooterID == -1 || shooter1.IsLinked)
                    continue;

                foreach (Shooter shooter2 in _allShooters)
                {
                    if (shooter1.Data.LinkedShooterID == shooter2.Data.ID)
                    {
                        LinkObject linkObject = Instantiate(_linkObjectPrefab);
                        linkObject.SetLink(shooter1, shooter2);

                        shooter1.SetLinked(shooter2, linkObject);
                        shooter2.SetLinked(shooter1, linkObject);
                        _linkObjects.Add(linkObject);
                    }
                }
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

        public bool CheckShooterCanJump(Shooter shooter, out bool withLinked)
        {
            withLinked = false;

            if (shooter.IsInConveyor)
                return false;

            if (!shooter.IsLinked)
                return shooter.IsInFirstPlace;

            Shooter linked = shooter.LinkedShooter;

            Debug.Assert(linked != null, "Linked shooter is null");
            if (linked == null || linked.IsInConveyor)
                return false;

            // Case 1: This shooter is in first place
            if (shooter.IsInFirstPlace)
            {
                // Linked is also in first place
                if (linked.IsInFirstPlace)
                {
                    withLinked = true;
                    return true;
                }

                // Linked is directly behind in the same lane
                ShooterLane shooterLane = _shooterLaneController.GetLane(shooter.Data.Coordinates.x);
                if (shooterLane != null && shooterLane.GetPositionInLane(linked) == 1)
                {
                    withLinked = true;
                    return true;
                }

                // Linked is elsewhere → this shooter cannot jump
                return false;
            }

            // Case 2: This shooter is NOT in first place, but linked partner is
            if (linked.IsInFirstPlace)
            {
                ShooterLane linkedLane = _shooterLaneController.GetLane(linked.Data.Coordinates.x);
                if (linkedLane != null && linkedLane.GetPositionInLane(shooter) == 1)
                {
                    // I'm directly behind my linked partner who is in front
                    withLinked = true;
                    return true;
                }
            }

            return false;
        }

        private void ShooterOnOnJumpRequest(Shooter shooter)
        {
            if (!CheckShooterCanJump(shooter, out bool withLinked))
                return;

            if (!withLinked)
            {
                OnShooterJumpRequest?.Invoke(shooter, false);
                return;
            }

            Shooter linkedShooter = shooter.LinkedShooter;
            LinkObject linkObject = shooter.LinkObject;

            // Determine who jumps first: the one in first place goes first
            Shooter first = shooter.IsInFirstPlace ? shooter : linkedShooter;
            Shooter second = shooter.IsInFirstPlace ? linkedShooter : shooter;

            shooter.BreakLink();
            linkedShooter.BreakLink();
            linkObject.BreakLink();

            OnShooterJumpRequest?.Invoke(first, false);

            DOVirtual.DelayedCall(GameConfigs.Instance.minShooterRequestInterval, () =>
            {
                if (second == null || second.IsInConveyor)
                    return;

                OnShooterJumpRequest?.Invoke(second, true);
            });
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

            if (shooter.Data.ColorId != targetObject.Data.ColorId)
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
            bullet.SetActive(false);
        }

        private void OnGetBullet(Bullet bullet)
        {
            bullet.gameObject.SetActive(true);
            bullet.SetActive(true);
        }

        private Bullet OnCreateBullet()
        {
            var bullet = Instantiate(_bulletPrefab, _bulletParent);
            bullet.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            bullet.AssignPool(_bulletPool);
            _bullets.Add(bullet);
            return bullet;
        }

        #endregion
    }
}