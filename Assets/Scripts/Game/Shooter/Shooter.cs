using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace Game
{
    public class Shooter : MonoBehaviour
    {
        [Title("References")]
        [SerializeField] private TextMeshPro _bulletCountText;

        [SerializeField] private Renderer _shooterRenderer;

        public bool IsHidden { get; private set; }

        public ShooterData Data { get; private set; }

        public bool IsInitialized { get; private set; }

        private void Initialize(ShooterData data)
        {
            IsInitialized = true;
        }

        public void SetData(ShooterData data)
        {
            Data = data;
            SetVisuals();
        }

        private void SetVisuals()
        {
            _bulletCountText.SetText(Data.BulletCount.ToString());
            _shooterRenderer.material = GetMaterial();
            if (Data.IsHidden)
                SetAsHidden();
        }

        private Material GetMaterial()
        {
            if (Data == null)
            {
                Debug.LogError("Shooter Data is null");
                return null;
            }

            return Data.Color switch
            {
                GameColor.Orange => ShooterVisualsConfigs.Instance.OrangeMaterial,
                GameColor.Green => ShooterVisualsConfigs.Instance.GreenMaterial,
                GameColor.Blue => ShooterVisualsConfigs.Instance.BlueMaterial,
                GameColor.Yellow => ShooterVisualsConfigs.Instance.YellowMaterial,
                _ => null
            };
        }

        private void SetAsHidden()
        {
            IsHidden = true;
            _shooterRenderer.material = ShooterVisualsConfigs.Instance.Hidden;
        }

        private void Reveal()
        {
        }
    }
}