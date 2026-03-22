using System;
using Game;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Utilities.EventBus;

namespace UI
{
    public class GameplayPanel : PanelBase
    {
        [Title("References")]
        [SerializeField] private Button _restartButton;
        [SerializeField] private Image _fillImage;
        [SerializeField] private TextMeshProUGUI _progressText;
        [SerializeField] private TextMeshProUGUI _levelText;

        private EventBinding<ProgressChangedEvent> _progressChangedEventBinding;

        public override void Initialize()
        {
            base.Initialize();
            _restartButton.onClick.AddListener(OnClickRestartButton);
            _progressChangedEventBinding = new EventBinding<ProgressChangedEvent>(OnProgressChanged);
            EventBus<ProgressChangedEvent>.Subscribe(_progressChangedEventBinding);
        }

        protected override void OnBeforeShow()
        {
            _fillImage.fillAmount = 0;
            int readableLevelIndex = LevelManager.Instance.ReadableLevelIndex;
            string levelIndicatorText = $"Level {readableLevelIndex}";
            _levelText.SetText(levelIndicatorText);
        }

        private void OnProgressChanged(ProgressChangedEvent e)
        {
            _fillImage.fillAmount = e.Progress;
            _progressText.SetText($"{e.CurrentCount}/{e.TotalCount}");
        }

        private void OnDestroy()
        {
            _restartButton.onClick.RemoveAllListeners();
            EventBus<ProgressChangedEvent>.Unsubscribe(_progressChangedEventBinding);
        }

        private void OnClickRestartButton()
        {
            GameManager.Instance.GameplayController.ChangeGameplayState(GameplayState.Gameplay);
        }
    }
}