using Sirenix.OdinInspector;
using UnityEngine;
using Utilities.EventBus;

namespace Game
{
    public class GameManager : Singleton<GameManager>
    {
        [Title("References")]
        [SerializeField] private GameConfigs _gameConfigs;
        [SerializeField] private ShooterVisualsConfigs _shooterVisualsConfigs;
        [SerializeField] private GameplayController _gameplayController;
        [SerializeField] private RemoteConfigManager _remoteConfigManager;

        public bool IsInitialized { get; private set; }
        public GameplayController GameplayController => _gameplayController;

        private EventBinding<GameplayStateChangedEvent> _gameplayStateChangedEventBinding;

        private void Start()
        {
            Application.targetFrameRate = 60;
            Initialize();
        }

        private void Initialize()
        {
            _gameplayStateChangedEventBinding = new EventBinding<GameplayStateChangedEvent>(OnGameplayStateChanged);
            EventBus<GameplayStateChangedEvent>.Subscribe(_gameplayStateChangedEventBinding);

            _gameConfigs.Initialize();
            _shooterVisualsConfigs.Initialize();
            _gameplayController.Initialize();

            IsInitialized = true;
            
            _remoteConfigManager.Initialize(OnRemoteConfigReady);
        }

        private void OnRemoteConfigReady()
        {
            PrepareForLevel();
            ChangeGameplayState(GameplayState.Gameplay);
        }

        private void OnDestroy()
        {
            EventBus<GameplayStateChangedEvent>.Unsubscribe(_gameplayStateChangedEventBinding);
        }

        private void OnGameplayStateChanged(GameplayStateChangedEvent e)
        {
            if (e.NewState == GameplayState.Fail)
            {
            }
            else if (e.NewState == GameplayState.Win)
            {
            }
            // Only re-prepare when transitioning FROM a terminal state (Win/Fail → restart).
            // Startup flow is handled by OnRemoteConfigReady to avoid a double-Prepare.
            else if (e.NewState == GameplayState.Gameplay &&
                     (e.OldState == GameplayState.Win || e.OldState == GameplayState.Fail))
            {
                PrepareForLevel();
            }
        }

        public void ChangeGameplayState(GameplayState newState)
        {
            _gameplayController.ChangeGameplayState(newState);
        }

        public void PrepareForLevel()
        {
            _gameplayController.Prepare();
        }
    }
}
