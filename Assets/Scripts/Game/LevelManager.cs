using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    public class LevelManager : Singleton<LevelManager>
    {
        [SerializeField] private List<LevelData> _levels = new List<LevelData>();

        public LevelData CurrentLevelData => _levels[RealLevelIndex];
        public int CurrentLevelIndex => PlayerPrefs.GetInt(GamePlayerPrefs.LevelPlayerPrefKey, 0);
        public int ReadableLevelIndex => CurrentLevelIndex + 1;
        public int RealLevelIndex => CurrentLevelIndex % _levels.Count;
        public bool IsInitialized { get; private set; }

        public bool IsPrepared { get; private set; }
        

        public void Initialize()
        {
            IsInitialized = true;
        }

        public void Prepare()
        {
            IsPrepared = true;
        }

    }
}