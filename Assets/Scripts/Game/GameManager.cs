using Dreamteck;
using Sirenix.OdinInspector;
using UnityEngine;

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

        protected override void Awake()
        {
            base.Awake();
            Initialize();
        }

        private void Initialize()
        {
            _gameConfigs.Initialize();
            _shooterVisualsConfigs.Initialize();
            _gameplayController.Initialize();
            IsInitialized = true;
        }
    }
}