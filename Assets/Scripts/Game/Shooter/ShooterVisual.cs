using TMPro;
using UnityEngine;

namespace Game
{
    public class ShooterVisual : MonoBehaviour
    {
        [SerializeField] private TextMeshPro _bulletCountText;
        [SerializeField] private Renderer _shooterRenderer;
        [SerializeField] private Transform _muzzleTransform;

        public Renderer ShooterRenderer => _shooterRenderer;
        public bool IsInitialized { get; private set; }

        private static readonly int BaseColorProp = Shader.PropertyToID("_BaseColor");
        private MaterialPropertyBlock _mpb;

        private ShooterData _shooterData;

        public void Initialize(ShooterData shooterData)
        {
            _shooterData = shooterData;
            SetDefaultVisuals();
            IsInitialized = true;
        }

        public void SetAsHidden()
        {
            if (_mpb == null)
                _mpb = new MaterialPropertyBlock();

            _mpb.SetColor(BaseColorProp, Color.white);
            _shooterRenderer.material = ShooterVisualsConfigs.Instance.Hidden;
            _shooterRenderer.SetPropertyBlock(_mpb);
            _bulletCountText.SetText("?");
        }

        public void SetColor(Color32 color)
        {
            if (_mpb == null)
                _mpb = new MaterialPropertyBlock();

            if (_shooterRenderer.sharedMaterial == null || _shooterRenderer.sharedMaterial == ShooterVisualsConfigs.Instance.Hidden)
                _shooterRenderer.sharedMaterial = ShooterVisualsConfigs.Instance.BaseMaterial;

            _shooterRenderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColorProp, color);
            _shooterRenderer.SetPropertyBlock(_mpb);
        }

        public void SetBulletCountText(int bulletCount)
        {
            _bulletCountText.SetText(bulletCount.ToString());
        }

        public void Reveal(Color32 color)
        {
            SetBulletCountText(_shooterData.BulletCount);
            SetColor(color);
        }

        public void Shoot(Bullet bullet, TargetObject target)
        {
            bullet.transform.position = _muzzleTransform.position;
            bullet.MoveTo(target);
            bullet.OnReachToTarget += Bullet_OnReachToTarget;
        }

        private void Bullet_OnReachToTarget(Bullet bullet, TargetObject targetObject)
        {
            targetObject.OnHit();
        }

        public void SetJumpableVisuals()
        {
            Color32 color = LevelManager.Instance.CurrentLevelData.GetColorById(_shooterData.ColorId);
            Reveal(color);
            _bulletCountText.alpha = 1f;
        }

        public void SetDefaultVisuals()
        {
            SetColor(LevelManager.Instance.CurrentLevelData.GetColorById(_shooterData.ColorId));
            SetBulletCountText(_shooterData.BulletCount);

            if (!_shooterData.IsHidden)
                _bulletCountText.alpha = 0.5f;
        }


        public void SetDefaultVisuals_Editor(LevelData levelData, ShooterData data)
        {
            _shooterData = data;
            Color32 color = levelData.GetColorById(_shooterData.ColorId);
            SetColor(color);

            SetBulletCountText(_shooterData.BulletCount);
            _bulletCountText.alpha = 1f;
        }
    }
}