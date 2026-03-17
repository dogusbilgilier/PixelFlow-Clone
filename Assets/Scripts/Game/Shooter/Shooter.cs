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

        public LinkObject LinkObject { get; private set; }
        public Shooter LinkedShooter { get; private set; }

        private int _currentBulletCount;
        private LevelData _levelData;

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

            IsInitialized = true;
        }

        private void OnDestroy()
        {
            OnJumpRequest = null;
            OnJumpToBoardCompleted = null;
            OnCompletedPath = null;
            OnBulletsExhausted = null;
        }

        public void SetData(ShooterData data, LevelData levelData = null)
        {
            Data = data;

            if (levelData != null)
                _levelData = levelData;
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
            RequestForJump();
        }

        public void RequestForJump()
        {
            OnJumpRequest?.Invoke(this);
        }

        public bool IsReadyForSearchTarget { get; private set; }

        public void JumpToBoard(ConveyorFollowerBoard board)
        {
            SetInConveyor(true);
            transform.SetParent(board.transform);
            transform.DOLocalJump(Vector3.zero, GameConfigs.Instance.shooterJumpToConveyorPower, 1, GameConfigs.Instance.shooterJumpToConveyorDuration);
            transform.DOLocalRotate(new Vector3(0, -90, 0), GameConfigs.Instance.shooterJumpToConveyorDuration).OnComplete(() =>
            {
                IsReadyForSearchTarget = true;
                OnJumpToBoardCompleted?.Invoke(this, board);
            });
            board.SetAssignedShooter(this);
            board.OnBoardCompletedPath += Board_OnBoardCompletedPath;
        }

        private void Board_OnBoardCompletedPath(ConveyorFollowerBoard board)
        {
            IsReadyForSearchTarget = false;
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


        public void SetCanJump(bool canJump)
        {
            CanJump = canJump;

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
    }
}