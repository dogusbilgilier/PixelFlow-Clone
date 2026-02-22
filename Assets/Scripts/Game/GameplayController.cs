using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game
{
    public class GameplayController : MonoBehaviour
    {
        [Title("References")]
        [SerializeField] private MainConveyor _mainConveyor;
        [SerializeField] private LevelManager _levelManager;

        [SerializeField] private Shooter _shooterPrefab;
        [SerializeField] private TargetObjectController _targetObjectController;
        [SerializeField] private ShooterController _shooterController;
        [SerializeField] private GridAndStorageVisualizer _gridAndStorageVisualizer;
        [SerializeField] private ShooterStorageController _shooterStorageController;

        public bool IsInitialized { get; private set; }

        public void Initialize()
        {
            _mainConveyor.Initialize();
            Debug.Assert(_mainConveyor.Bounds.HasValue, "Main Conveyor Doesn't Have Bounds");

            _levelManager.Initialize(_mainConveyor.Bounds.Value);
            _shooterController.Initialize(_mainConveyor.Bounds.Value);
            _targetObjectController.Initialize(_mainConveyor.Bounds.Value);
            _gridAndStorageVisualizer.Initialize(_shooterController.ShooterGrid);
            _shooterStorageController.Initialize(_gridAndStorageVisualizer.StorageVisualPieces);

            _shooterController.OnShooterJumpRequest += ShooterController_OnShooterJumpRequest;
            _shooterController.OnShooterCompletedPath += ShooterController_OnShooterCompletedPath;
            _shooterController.OnShooterDestroyed += ShooterController_OnShooterDestroyed;

            IsInitialized = true;
        }

        private void ShooterController_OnShooterDestroyed(Shooter shooter)
        {
            _shooterController.RemoveMovingShooter(shooter);
            _mainConveyor.ShooterDestroyed(shooter);
        }

        private void ShooterController_OnShooterJumpRequest(Shooter shooter)
        {
            if (_mainConveyor.TryGetAvailableBoard(out ConveyorFollowerBoard board))
            {
                _mainConveyor.BoardToConveyor();
                shooter.JumpToBoard(board);
                _shooterController.AddMovingShooter(shooter);

                if (_shooterStorageController.IsShooterInStorage(shooter, out StoragePiece storage))
                {
                    storage.Unassign();
                    _shooterStorageController.ArrangeStorageShooters();
                }
                else
                {
                    _shooterController.ShooterJumpToConveyorFromLane(shooter);
                }
            }
        }

        private void ShooterController_OnShooterCompletedPath(Shooter shooter)
        {
            _shooterController.RemoveMovingShooter(shooter);

            if (shooter.IsBulletsExhausted)
                return;

            if (!_shooterStorageController.TryConsumeShooter(shooter))
            {
                //TODO FAIL
                Debug.Log("FAIL");
                shooter.transform.parent = null;
            }
            else
            {
                shooter.SetInConveyor(false);
            }
        }

        private void Update()
        {
            if (!IsInitialized)
                return;

            if (_shooterController.CurrentlyMovingShooters is not { Count: > 0 })
                return;

            for (var i = _shooterController.CurrentlyMovingShooters.Count - 1; i >= 0; i--)
            {
                var shooter = _shooterController.CurrentlyMovingShooters[i];
                _targetObjectController.CheckForShooter(shooter);
            }
        }
    }
}