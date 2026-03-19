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
            OnBeforeShow();
            gameObject.SetActive(true);
            _canvasGroup.alpha = 1;
            _canvasGroup.interactable = true;
            _canvasGroup.blocksRaycasts = true;
            OnShown();
        }

        protected virtual void OnBeforeShow()
        {
        }

        protected virtual void OnShown()
        {
        }

        public void HidePanel()
        {
            OnBeforeHide();
            gameObject.SetActive(false);
            _canvasGroup.alpha = 0;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
            OnHidden();
        }

        protected virtual void OnBeforeHide()
        {
        }

        protected virtual void OnHidden()
        {
        }
    }
}