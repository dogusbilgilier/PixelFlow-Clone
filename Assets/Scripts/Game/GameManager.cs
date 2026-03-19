using System;
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

        public bool IsInitialized { get; private set; }
        public GameplayController GameplayController => _gameplayController;

        private EventBinding<GameplayStateChangedEvent> _gameplayStateChangedEventBinding;

        private void Start()
        {
            Application.targetFrameRate = 60;
            Initialize();
            PrepareForLevel();
        }

        private void Initialize()
        {
            _gameplayStateChangedEventBinding = new EventBinding<GameplayStateChangedEvent>(OnGameplayStateChanged);
            EventBus<GameplayStateChangedEvent>.Subscribe(_gameplayStateChangedEventBinding);

            _gameConfigs.Initialize();
            _shooterVisualsConfigs.Initialize();
            _gameplayController.Initialize();
            IsInitialized = true;
        }

        private void OnDestroy()
        {
            EventBus<GameplayStateChangedEvent>.Unsubscribe(_gameplayStateChangedEventBinding);
        }

        private void OnGameplayStateChanged(GameplayStateChangedEvent e)
        {
            if (e.newState == GameplayState.Fail)
            {
            }
            else if (e.newState == GameplayState.Win)
            {
            }
            else if (e.newState == GameplayState.Gameplay)
            {
                PrepareForLevel();
            }
        }

        public void PrepareForLevel()
        {
            _gameplayController.Prepare();
        }
    }
}