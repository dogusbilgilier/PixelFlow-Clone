using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class LevelFailedPanel : PanelBase
    {
        [Title("References")]
        [SerializeField] private Button _restartButton;

        public override void Initialize()
        {
            base.Initialize();
            _restartButton.onClick.AddListener(OnClickRestartButton);
        }

        private void OnDestroy()
        {
            _restartButton.onClick.RemoveAllListeners();
        }

        private void OnClickRestartButton()
        {
            // TODO RESTART LEVEL
        }
    }
}