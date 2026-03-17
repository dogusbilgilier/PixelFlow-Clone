using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using Utilities.EventBus;

namespace UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public class UIManager : MonoBehaviour
    {
        [Title("References")]
        [SerializeField] private GameplayPanel _gameplayPanel;
        private List<PanelBase> _panels;

        private EventBinding<GameplayStateChangedEvent> _gameplayStateChangedEvent;

        private void Awake()
        {
            _panels = new List<PanelBase>() { _gameplayPanel };

            _gameplayStateChangedEvent = new EventBinding<GameplayStateChangedEvent>(OnGameplayStateChangedEvent);
            EventBus<GameplayStateChangedEvent>.Subscribe(_gameplayStateChangedEvent);

            foreach (var panel in _panels)
                panel.Initialize();
        }

        private void OnDestroy()
        {
            EventBus<GameplayStateChangedEvent>.Unsubscribe(_gameplayStateChangedEvent);
        }

        private void OnGameplayStateChangedEvent(GameplayStateChangedEvent gameplayStateChangedEvent)
        {
            if (gameplayStateChangedEvent.newState == GameplayState.Gameplay)
            {
                _gameplayPanel.ShowPanel();
            }
        }
    }
}