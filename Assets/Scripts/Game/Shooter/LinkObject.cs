using UnityEngine;

namespace Game
{
    public class LinkObject : MonoBehaviour
    {
        [SerializeField] private MeshRenderer _meshRenderer;

        private Shooter _shooter1;
        private Shooter _shooter2;

        private bool _isLinkActive;

        public void SetLink(Shooter shooter1, Shooter shooter2)
        {
            _shooter1 = shooter1;
            _shooter2 = shooter2;
            MaterialPropertyBlock block1 = new MaterialPropertyBlock();
            MaterialPropertyBlock block2 = new MaterialPropertyBlock();
            block1.SetColor("_BaseColor", LevelManager.Instance.CurrentLevelData.GetColorById(shooter1.Data.ColorId));
            block2.SetColor("_BaseColor", LevelManager.Instance.CurrentLevelData.GetColorById(shooter2.Data.ColorId));
            _meshRenderer.SetPropertyBlock(block1, 0);
            _meshRenderer.SetPropertyBlock(block2, 1);
            _isLinkActive = true;
        }

        public void BreakLink()
        {
            _isLinkActive = false;
            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!_isLinkActive)
                return;

            transform.position = (_shooter2.transform.position + _shooter1.transform.position) / 2f + Vector3.up * 0.5f;
            float distance = Vector3.Distance(_shooter1.transform.position, _shooter2.transform.position);
            Vector3 scale = new Vector3(0.5f, 0.5f, distance);
            transform.localScale = scale;
            transform.forward = ((_shooter1.transform.position) - _shooter2.transform.position).normalized;
        }
    }
}