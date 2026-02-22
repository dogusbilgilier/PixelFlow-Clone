using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game
{
    public class ShooterController : MonoBehaviour
    {
        [Title("References")]
        [SerializeField] private Shooter _shooterPrefab;
        [SerializeField] private Transform _shooterParent;
        public bool IsInitialized { get; private set; }
        public GameGrid ShooterGrid => _shooterAreaGrid;

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
    }
}