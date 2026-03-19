using System;
using UnityEngine;

namespace Game
{
    public class TargetObject : MonoBehaviour
    {
        [SerializeField] private Renderer _renderer;
        public TargetData Data { get; private set; }
        public bool IsDestroyed { get; private set; }

        public bool IsInitialized { get; private set; }

        private static readonly int BaseColorProp = Shader.PropertyToID("_BaseColor");
        private MaterialPropertyBlock _materialPropertyBlock;
        private LevelData _levelData;

        public event Action<TargetObject> OnTargetHit; 
        public void Initialize(TargetData data)
        {
            Data = data;
            SetData(Data);
            IsInitialized = true;
        }
        private void OnDestroy()
        {
            OnTargetHit = null;
        }
        public void SetData(TargetData data, LevelData levelData = null)
        {
            Data = data;

            if (levelData != null)
                _levelData = levelData;

            SetVisuals();
        }

        private void SetVisuals()
        {
            if (Data == null)
            {
                Debug.LogError("Target Data is null");
                return;
            }

            LevelData ld = _levelData;

#if !UNITY_EDITOR
            if (ld == null)
                ld = LevelManager.Instance.CurrentLevelData;
#else
            if (ld == null && Application.isPlaying)
                ld = LevelManager.Instance.CurrentLevelData;
#endif

            if (ld == null)
                return;

            if (_materialPropertyBlock == null)
                _materialPropertyBlock = new MaterialPropertyBlock();

            if (_renderer.sharedMaterial == null)
                _renderer.sharedMaterial = ShooterVisualsConfigs.Instance.BaseMaterial;

            _renderer.GetPropertyBlock(_materialPropertyBlock);
            Color32 color = ld.GetColorById(Data.ColorId);
            _materialPropertyBlock.SetColor(BaseColorProp, color);
            _renderer.SetPropertyBlock(_materialPropertyBlock);
        }

        public void OnHit()
        {
            gameObject.SetActive(false);
            OnTargetHit?.Invoke(this);
        }

        public void MarketForHit()
        {
            IsDestroyed = true;
        }

     
    }
}