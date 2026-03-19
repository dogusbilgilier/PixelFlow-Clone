using Game;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class LevelCompletedPanel : PanelBase
    {
        [Title("References")]
        [SerializeField] private Button _nextLevelButton;

        public override void Initialize()
        {
            base.Initialize();
            _nextLevelButton.onClick.AddListener(OnClickRestartButton);
        }

        private void OnDestroy()
        {
            _nextLevelButton.onClick.RemoveAllListeners();
        }

        private void OnClickRestartButton()
        {
           GameManager.Instance.GameplayController.ChangeGameplayState(GameplayState.Gameplay);
        }
    }
}