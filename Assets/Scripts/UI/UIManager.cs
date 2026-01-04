using System;
using UnityEngine;
using Utilities.EventBus;

namespace UI
{
    public class UIManager : MonoBehaviour
    {
        private EventBinding<GameplayStateChangedEvent> _gameplayStateChangedEvent;

        private void Awake()
        {
            _gameplayStateChangedEvent = new EventBinding<GameplayStateChangedEvent>(OnGameplayStateChangedEvent);
            EventBus<GameplayStateChangedEvent>.Subscribe(_gameplayStateChangedEvent);
        }

        private void OnDestroy()
        {
            EventBus<GameplayStateChangedEvent>.Unsubscribe(_gameplayStateChangedEvent);
        }

        private void OnGameplayStateChangedEvent(GameplayStateChangedEvent gameplayStateChangedEvent)
        {
            
        }
    }
}