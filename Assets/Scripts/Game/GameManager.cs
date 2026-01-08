using Dreamteck;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game
{
    public class GameManager : Singleton<GameManager>
    {
        [Title("References")]
        [SerializeField] private GameConfigs _gameConfigs;

        [SerializeField] private GameplayController _gameplayController;
        public bool IsInitialized { get; private set; }
        public GameplayController GameplayController => _gameplayController;

        private void Awake()
        {
            Initialize();
        }

        private void Initialize()
        {
            _gameConfigs.Initialize();
            _gameplayController.Initialize();
            IsInitialized = true;
        }
    }
}