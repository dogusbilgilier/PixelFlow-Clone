using Sirenix.OdinInspector;
using UnityEngine;

namespace UI
{
    public abstract class PanelBase : MonoBehaviour
    {
        [Title("References")]
        [SerializeField] private CanvasGroup _canvasGroup;

        public bool IsInitialized { get; private set; }

        public virtual void Initialize()
        {
            IsInitialized = true;
        }

        public void ShowPanel()
        {
            gameObject.SetActive(true);
            _canvasGroup.alpha = 1;
            _canvasGroup.interactable = true;
            _canvasGroup.blocksRaycasts = true;
        }

        public void HidePanel()
        {
            gameObject.SetActive(false);
            _canvasGroup.alpha = 0;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
        }
    }
}