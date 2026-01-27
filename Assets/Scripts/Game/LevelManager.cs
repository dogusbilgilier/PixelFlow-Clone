using UnityEngine;

namespace Game
{
    public class LevelManager : Singleton<LevelManager>
    {
        public bool IsInitialized { get; private set; }

        [SerializeField] private LevelData _levelData;
        public LevelData CurrentLevelData => _levelData;
        private Bounds _mainConveyorBounds;

        public void Initialize(Bounds mainConveyorBounds)
        {
            _mainConveyorBounds = mainConveyorBounds;
            IsInitialized = true;
        }

        public void CreateLevel()
        {
            
        }
    }
}