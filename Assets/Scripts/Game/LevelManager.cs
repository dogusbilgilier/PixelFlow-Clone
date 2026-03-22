using System;
using System.Collections.Generic;
using UnityEngine;
using Utilities.EventBus;

namespace Game
{
    public class LevelManager : Singleton<LevelManager>
    {
        [SerializeField] private List<LevelData> _levels = new List<LevelData>();

        public LevelData CurrentLevelData => _levels[RealLevelIndex];

        private int CurrentLevelIndex
        {
            get => PlayerPrefs.GetInt(GamePlayerPrefs.LevelPlayerPrefKey, 0);
            set => PlayerPrefs.SetInt(GamePlayerPrefs.LevelPlayerPrefKey, value);
        }
        public int ReadableLevelIndex => CurrentLevelIndex + 1;
        public int RealLevelIndex => CurrentLevelIndex % _levels.Count;
        public bool IsInitialized { get; private set; }

        public bool IsPrepared { get; private set; }


        private EventBinding<GameplayStateChangedEvent> _gameplayStateChangedEventBinding;
        
        public void Initialize()
        {
            _gameplayStateChangedEventBinding = new EventBinding<GameplayStateChangedEvent>(OnGameplayStateChangedEvent);
            EventBus<GameplayStateChangedEvent>.Subscribe(_gameplayStateChangedEventBinding);
            IsInitialized = true;
        }

        private void OnGameplayStateChangedEvent(GameplayStateChangedEvent e)
        {
            if (e.NewState == GameplayState.Win)
                CurrentLevelIndex++;
        }

        private void OnDestroy()
        {
            EventBus<GameplayStateChangedEvent>.Unsubscribe(_gameplayStateChangedEventBinding);
        }

        public void Prepare()
        {
            IsPrepared = true;
        }
    }
}