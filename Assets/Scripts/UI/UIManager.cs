using System;
using System.Collections.Generic;
using Game;
using Sirenix.OdinInspector;
using UnityEngine;
using Utilities.EventBus;

namespace UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public class UIManager : MonoBehaviour
    {
        [Title("References - Panels")]
        [SerializeField] private GameplayPanel _gameplayPanel;
        [SerializeField] private LevelCompletedPanel _levelCompletedPanel;
        [SerializeField] private LevelFailedPanel _levelFailedPanel;
        private List<PanelBase> _panels;

        private EventBinding<GameplayStateChangedEvent> _gameplayStateChangedEvent;

        private void Awake()
        {
            _panels = new List<PanelBase>() { _gameplayPanel, _levelCompletedPanel ,_levelFailedPanel};

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
            if (gameplayStateChangedEvent.NewState == GameplayState.Gameplay)
            {
                ShowPanel(_gameplayPanel);
            }
            else if (gameplayStateChangedEvent.NewState == GameplayState.Win)
            {
                ShowPanel(_levelCompletedPanel);
            }
            else if (gameplayStateChangedEvent.NewState == GameplayState.Fail)
            {
                ShowPanel(_levelFailedPanel);
            }
        }

        private void ShowPanel(PanelBase panelToShow)
        {
            foreach (var panel in _panels)
            {
                if (panelToShow == panel)
                {
                    panel.ShowPanel();
                    continue;
                }
                panel.HidePanel();
            }
            
        }
    }
}