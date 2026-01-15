using UnityEngine;

namespace Game
{
    public class TargetObject : MonoBehaviour
    {
        [SerializeField] private Renderer _renderer;
        public TargetData Data { get; private set; }

        public bool IsInitialized { get; private set; }

        private void Initialize(TargetData data)
        {
            Data = data;
            IsInitialized = true;
        }

        public void SetData(TargetData data)
        {
            Data = data;
            SetVisuals();
        }

        private void SetVisuals()
        {
            _renderer.material = GetMaterial();
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
    }
}