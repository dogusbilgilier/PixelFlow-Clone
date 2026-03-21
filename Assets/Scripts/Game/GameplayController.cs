using Sirenix.OdinInspector;
using Unity.VisualScripting;
using UnityEngine;
using Utilities.EventBus;

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

        private GameplayState _gameplayState;
        private float _lastShooterSentTime = 0f;
        public bool IsInitialized { get; private set; }
        public bool IsPrepared { get; private set; }

        public void Initialize()
        {
            _levelManager.Initialize();
            _mainConveyor.Initialize();
            Debug.Assert(_mainConveyor.Bounds.HasValue, "Main Conveyor Doesn't Have Bounds");

            _shooterController.Initialize(_mainConveyor.Bounds.Value);
            _targetObjectController.Initialize(_mainConveyor.Bounds.Value);

            _gridAndStorageVisualizer.Initialize(_shooterController.ShooterGrid);
            _shooterStorageController.Initialize();

            ChangeGameplayState(GameplayState.Gameplay);

            _shooterController.OnShooterJumpRequest += ShooterController_OnShooterJumpRequest;
            _shooterController.OnShooterCompletedPath += ShooterController_OnShooterCompletedPath;
            _shooterController.OnShooterDestroyed += ShooterController_OnShooterDestroyed;
            _shooterController.OnAllShootersCompleted += ShooterController_OnAllShootersCompleted;

            _targetObjectController.OnAllTargetsDestroyed += TargetObjectController_OnAllTargetsDestroyed;

            IsInitialized = true;
        }

        public void Prepare()
        {
            IsPrepared = false;
            
            _levelManager.Prepare();
            _mainConveyor.Prepare();
            _shooterController.Prepare();
            _targetObjectController.Prepare();

            _gridAndStorageVisualizer.Prepare(_shooterController.ShooterGrid);
            _shooterStorageController.Prepare(_gridAndStorageVisualizer.StorageVisualPieces);

            IsPrepared = true;
        }

        private void Update()
        {
            if (!IsInitialized)
                return;

            CheckTargetsForShooters();
        }

        private void CheckTargetsForShooters()
        {
            if (_shooterController.CurrentlyMovingShooters is not { Count: > 0 })
                return;

            for (int i = _shooterController.CurrentlyMovingShooters.Count - 1; i >= 0; i--)
            {
                Shooter shooter = _shooterController.CurrentlyMovingShooters[i];

                if (!shooter.IsReadyForSearchForTarget)
                    continue;

                if (!_targetObjectController.TryFindTargetForShooter(shooter, out var targetObjects, out var side))
                    continue;

                foreach (TargetObject targetObject in targetObjects)
                {
                    if (_shooterController.TryShootForTarget(shooter, targetObject, side))
                        targetObject.MarketForHit();
                }
            }
        }

        public void ChangeGameplayState(GameplayState newState)
        {
            GameplayState oldState = _gameplayState;
            _gameplayState = newState;

            EventBus<GameplayStateChangedEvent>.Fire(new GameplayStateChangedEvent()
            {
                OldState = oldState,
                NewState = _gameplayState
            });

            Debug.Log("Gameplay state changed to " + newState);
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
                ChangeGameplayState(GameplayState.Fail);
                EventBus<GameplayStateChangedEvent>.Fire(new GameplayStateChangedEvent());
                shooter.transform.parent = null;
            }
            else
            {
                shooter.SetInConveyor(false);
            }
        }

        private void ShooterController_OnShooterDestroyed(Shooter shooter)
        {
            _shooterController.RemoveMovingShooter(shooter);
            _mainConveyor.ShooterDestroyed(shooter);
        }

        private void ShooterController_OnShooterJumpRequest(Shooter shooter, bool skipInterval)
        {
            if (!_mainConveyor.TryGetAvailableBoard(out ConveyorFollowerBoard board))
                return;

            if (!skipInterval && Time.time < (_lastShooterSentTime + GameConfigs.Instance.minShooterRequestInterval))
                return;

            _lastShooterSentTime = Time.time;

            _mainConveyor.BoardToConveyor(board);
            _shooterController.AddMovingShooter(shooter);

            shooter.JumpToBoard(board);
            shooter.OnJumpToBoardCompleted += Shooter_OnJumpToBoardCompleted;

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

        private void Shooter_OnJumpToBoardCompleted(Shooter shooter, ConveyorFollowerBoard board)
        {
            shooter.OnJumpToBoardCompleted -= Shooter_OnJumpToBoardCompleted;
            board.StartMove();
        }

        private void TargetObjectController_OnAllTargetsDestroyed()
        {
            Debug.Log("WIN");
            ChangeGameplayState(GameplayState.Win);
        }

        private void ShooterController_OnAllShootersCompleted()
        {
            Debug.Log("AllShootersCompleted");
        }
    }
}