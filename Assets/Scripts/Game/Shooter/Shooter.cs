using System;
using System.Collections.Generic;
using Game;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game
{
    public class Shooter : MonoBehaviour
    {
        [Title("References")]
        [SerializeField] private TextMeshPro _bulletCountText;
        [SerializeField] private Renderer _shooterRenderer;
        [SerializeField] private ShooterVisual _shooterVisual;
        public ShooterVisual ShooterVisual => _shooterVisual;

        public ShooterTargetData ShooterTargetData { get; private set; }
        public ShooterData Data { get; private set; }

        public bool IsLinked { get; private set; }
        public bool IsHidden { get; private set; }
        public bool IsInConveyor { get; private set; }
        public bool IsInFirstPlace { get; private set; }
        public bool IsBulletsExhausted { get; private set; }
        public bool IsInitialized { get; private set; }

        private int _currentBulletCount;
        private LevelData _levelData;

        //EVENTS
        public event Action<Shooter> OnJumpRequest;
        public event Action<Shooter> OnCompletedPath;

        public event Action<Shooter> OnBulletsExhausted;

        //-------------
        public void Initialize(ShooterData data)
        {
            Data = data;
            SetData(Data);
            _shooterVisual.Initialize();

            ShooterTargetData = new ShooterTargetData();
            _currentBulletCount = Data.BulletCount;

            IsInitialized = true;
        }

        private void OnDestroy()
        {
            OnJumpRequest = null;
            OnCompletedPath = null;
            OnBulletsExhausted = null;
        }

        public void SetData(ShooterData data, LevelData levelData = null)
        {
            Data = data;

            if (levelData != null)
                _levelData = levelData;

#if UNITY_EDITOR
            SetEditorVisuals();
#else
            SetGameVisuals();
#endif
        }

        private Color32 ResolveColor()
        {
            LevelData ld = _levelData;

#if !UNITY_EDITOR
            if (ld == null)
                ld = LevelManager.Instance.CurrentLevelData;
#else
            if (ld == null && Application.isPlaying)
                ld = LevelManager.Instance.CurrentLevelData;
#endif

            if (ld != null)
                return ld.GetColorById(Data.ColorId);

            return new Color32(255, 255, 255, 255);
        }

        private void SetGameVisuals()
        {
            _shooterVisual.SetBulletCountText(_currentBulletCount);
            _shooterVisual.SetColor(ResolveColor());

            if (Data.IsHidden)
                SetAsHidden();

            if (Data.LinkedShooterID != -1)
            {
                //TODO: Set Link Visuals
            }
        }

        private void SetEditorVisuals()
        {
            _shooterVisual.SetBulletCountText(Data.BulletCount);
            _shooterVisual.SetColor(ResolveColor());

            if (Data.IsHidden)
                SetAsHidden();

            if (Data.LinkedShooterID != -1)
            {
                //TODO: Set Link Visuals
            }
        }

        private void SetAsHidden()
        {
            IsHidden = true;
            _shooterVisual.SetAsHidden();
        }

        private void OnMouseDown()
        {
            if (!CanJumpToConveyor())
                return;

            OnJumpRequest?.Invoke(this);
        }

        private bool CanJumpToConveyor()
        {
            return !IsInConveyor && IsInFirstPlace;
        }

        public void JumpToBoard(ConveyorFollowerBoard board)
        {
            SetInConveyor(true);
            transform.SetParent(board.transform);
            transform.localPosition = Vector3.zero;
            transform.localEulerAngles = new Vector3(0, -90, 0);
            board.SetAssignedShooter(this);
            board.OnBoardCompletedPath += Board_OnBoardCompletedPath;
        }

        private void Board_OnBoardCompletedPath(ConveyorFollowerBoard board)
        {
            board.OnBoardCompletedPath -= Board_OnBoardCompletedPath;
            ShooterTargetData.Reset();
            OnCompletedPath?.Invoke(this);
        }

        public void JumpToStorage(GridPiece storage)
        {
            transform.SetParent(storage.transform);
            transform.localPosition = Vector3.zero;
            transform.localEulerAngles = new Vector3(0, 0, 0);
        }

        public void SetInFirst()
        {
            IsInFirstPlace = true;

            if (IsHidden)
                _shooterVisual.Reveal(ResolveColor());
        }

        public void SetInConveyor(bool inConveyor)
        {
            IsInConveyor = inConveyor;
        }

        public void OnShoot(TargetObject targetObject, Side side, Bullet bulletToShoot)
        {
            ShooterTargetData.AddTargetData(side, targetObject.Data.Coordinates);
            _currentBulletCount--;

            if (_currentBulletCount <= 0)
            {
                _shooterVisual.SetBulletCountText(_currentBulletCount);
                gameObject.SetActive(false);
                BulletsExhausted();
            }

            _shooterVisual.Shoot(bulletToShoot, targetObject);
            _shooterVisual.SetBulletCountText(_currentBulletCount);
        }

        private void BulletsExhausted()
        {
            IsInConveyor = false;
            IsInFirstPlace = false;
            IsBulletsExhausted = true;
            OnBulletsExhausted?.Invoke(this);
        }
    }
}

