using TMPro;
using UnityEngine;

namespace Game
{
    public class ShooterVisual : MonoBehaviour
    {
        [SerializeField] private TextMeshPro _bulletCountText;
        [SerializeField] private Renderer _shooterRenderer;
        [SerializeField] private Transform _muzzleTransform;

        public bool IsInitialized { get; private set; }

        public void Initialize()
        {
            IsInitialized = true;
        }

        public void SetAsHidden()
        {
            _shooterRenderer.material = ShooterVisualsConfigs.Instance.Hidden;
        }

        public void SetMaterial(GameColor color)
        {
            _shooterRenderer.material = GetMaterial(color);
        }

        private Material GetMaterial(GameColor color, bool isHidden = false)
        {
            if (isHidden)
                return ShooterVisualsConfigs.Instance.Hidden;

            return color switch
            {
                GameColor.Orange => ShooterVisualsConfigs.Instance.OrangeMaterial,
                GameColor.Green => ShooterVisualsConfigs.Instance.GreenMaterial,
                GameColor.Blue => ShooterVisualsConfigs.Instance.BlueMaterial,
                GameColor.Yellow => ShooterVisualsConfigs.Instance.YellowMaterial,
                _ => null
            };
        }

        public void SetBulletCountText(int bulletCount)
        {
            _bulletCountText.SetText(bulletCount.ToString());
        }

        public void Reveal(GameColor color)
        {
            SetMaterial(color);
        }

        public void Shoot(Bullet bullet, TargetObject target)
        {
            bullet.transform.position = _muzzleTransform.position;
            bullet.MoveTo(target);
            bullet.OnReachToTarget += Bullet_OnReachToTarget;
        }

        private void Bullet_OnReachToTarget(Bullet bullet,TargetObject targetObject)
        {
            targetObject.OnHit();
        }
    }
}