using System;
using System.Collections.Generic;
using DG.Tweening;
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
        public bool CanJump { get; private set; }
        public bool IsReadyForSearchForTarget { get; private set; }
        public LinkObject LinkObject { get; private set; }
        public Shooter LinkedShooter { get; private set; }

        private int _currentBulletCount;
        private LevelData _levelData;
        private Transform _parentTransform;
        private Tweener _rejectTween;
        private Tweener _recoilTween;
        private Sequence _jumpSequence;
        private Vector3 _originalScale;

        //EVENTS
        public event Action<Shooter> OnJumpRequest;
        public event Action<Shooter, ConveyorFollowerBoard> OnJumpToBoardCompleted;
        public event Action<Shooter> OnCompletedPath;
        public event Action<Shooter> OnBulletsExhausted;

        //-------------
        public void Initialize(ShooterData data)
        {
            Data = data;
            SetData(Data);

            _shooterVisual.Initialize(Data);
            SetVisuals();

            ShooterTargetData = new ShooterTargetData();
            _currentBulletCount = Data.BulletCount;

            _parentTransform = transform.parent;
            _originalScale = _shooterVisual.transform.localScale;
         
            IsInitialized = true;
        }

        private void OnDestroy()
        {
            OnJumpRequest = null;
            OnJumpToBoardCompleted = null;
            OnCompletedPath = null;
            OnBulletsExhausted = null;

            _jumpSequence?.Kill();
            _jumpSequence = null;
            DOTween.Kill(transform);
            DOTween.Kill(_shooterVisual.transform);
        }

        public void SetData(ShooterData data)
        {
            Data = data;
        }

        private void SetVisuals()
        {
            _shooterVisual.SetDefaultVisuals();
            
            if (Data.IsHidden)
                SetAsHidden();
        }

        public void SetLinked(Shooter linkedShooter, LinkObject linkObject)
        {
            IsLinked = true;
            LinkedShooter = linkedShooter;
            LinkObject = linkObject;
        }

        public void BreakLink()
        {
            IsLinked = false;
            LinkedShooter = null;
        }

        private void SetAsHidden()
        {
            IsHidden = true;
            _shooterVisual.SetAsHidden();
        }

        private void OnMouseDown()
        {
            OnJumpRequest?.Invoke(this);
        }

        public void JumpToBoard(ConveyorFollowerBoard board)
        {
            SetInConveyor(true);
            CanJump = false;
            _jumpSequence?.Kill();
            DOTween.Kill(transform);

            transform.SetParent(board.transform);

            float duration = GameConfigs.Instance.shooterJumpToConveyorDuration;
            float power = GameConfigs.Instance.shooterJumpToConveyorPower;

            _jumpSequence = DOTween.Sequence();
            _jumpSequence.Insert(0f, transform.DOLocalJump(Vector3.zero, power, 1, duration));
            _jumpSequence.Insert(0f, transform.DOLocalRotate(new Vector3(0, -90, 0), duration));
            _jumpSequence.OnComplete(() =>
            {
                _jumpSequence = null;
                IsReadyForSearchForTarget = true;
                OnJumpToBoardCompleted?.Invoke(this, board);
            });
            _jumpSequence.SetLink(gameObject);

            board.SetAssignedShooter(this);
            board.OnBoardCompletedPath += Board_OnBoardCompletedPath;
        }

        public void JumpToStorage(GridPiece storage)
        {
            DOTween.Kill(transform);
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
            _shooterVisual.Shoot(bulletToShoot, targetObject);
            _shooterVisual.SetBulletCountText(_currentBulletCount);

            DoRecoil();

            if (_currentBulletCount <= 0)
            {
                _shooterVisual.SetBulletCountText(_currentBulletCount);
                gameObject.SetActive(false);
                BulletsExhausted();
            }
        }

        private void DoRecoil()
        {
            _rejectTween?.Kill();
            _recoilTween?.Kill();
            _shooterVisual.transform.localScale = _originalScale;
            _recoilTween = _shooterVisual.transform.DOShakeScale(0.1f, Vector3.one * 0.1f, 0, 90f, true, ShakeRandomnessMode.Harmonic)
                .OnKill(() => { transform.localScale = _originalScale; })
                .SetLink(gameObject);
        }

        public void DoRejectAnim()
        {
            _recoilTween?.Kill();
            _rejectTween?.Kill();
            _rejectTween = transform.DOShakeRotation(0.3f, Vector3.up * 15, 20, 90f, true, ShakeRandomnessMode.Harmonic)
                .OnComplete(() => { transform.localRotation = Quaternion.identity; })
                .OnKill(() => { transform.localRotation = Quaternion.identity; })
                .SetLink(gameObject);
        }

        private void BulletsExhausted()
        {
            IsInConveyor = false;
            IsInFirstPlace = false;
            IsBulletsExhausted = true;
            OnBulletsExhausted?.Invoke(this);
        }

        public void SetCanJump(bool canJump)
        {
            if (CanJump == canJump)
                return;

            CanJump = canJump;

            // Don't change visuals of hidden shooters
            if (IsHidden)
                return;

            if (canJump)
                _shooterVisual.SetJumpableVisuals();
            else
                _shooterVisual.SetDefaultVisuals();
        }

        private Color32 ResolveColor()
        {
            LevelData levelData = _levelData;

            if (levelData == null && Application.isPlaying)
                levelData = LevelManager.Instance.CurrentLevelData;

            if (levelData != null)
                return levelData.GetColorById(Data.ColorId);

            return new Color32(255, 255, 255, 255);
        }

        public void SetVisuals_Editor(LevelData levelData)
        {
            _shooterVisual.SetDefaultVisuals_Editor(levelData, Data);
            if (Data.IsHidden)
                SetAsHidden();
        }

        public void ResetParent()
        {
            transform.SetParent(_parentTransform);
        }

        private void Board_OnBoardCompletedPath(ConveyorFollowerBoard board)
        {
            IsReadyForSearchForTarget = false;
            board.OnBoardCompletedPath -= Board_OnBoardCompletedPath;
            ShooterTargetData.Reset();
            OnCompletedPath?.Invoke(this);
        }
    }
}