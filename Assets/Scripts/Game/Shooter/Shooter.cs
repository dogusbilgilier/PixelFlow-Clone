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

        public ShooterTargetData ShooterTargetData { get; private set; }

        public bool IsLinked { get; private set; }
        public bool IsHidden { get; private set; }
        public ShooterData Data { get; private set; }
        public bool IsInConveyor { get; private set; }
        public bool IsInFirstPlace { get; private set; }
        public bool IsBulletsExhausted { get; private set; }
        public bool IsInitialized { get; private set; }

        private int _currentBulletCount;

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

        public void SetData(ShooterData data)
        {
            Data = data;
            SetVisuals();
        }

        private void SetVisuals()
        {
            //TODO Make visual changes in ShooterVisuals class
            _bulletCountText.SetText(Data.BulletCount.ToString());
            _shooterRenderer.material = GetMaterial();

            if (Data.IsHidden)
                SetAsHidden();

            if (Data.LinkedShooterID != -1)
            {
                //TODO: Set Link Visuals
            }
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
        }

        public void SetInConveyor(bool inConveyor)
        {
            IsInConveyor = inConveyor;
        }

        public void OnShootToTarget(TargetObject targetObject, Side side)
        {
            if (IsBulletsExhausted)
                return;

            ShooterTargetData.AddTargetData(side, targetObject.Data.Coordinates);
            _currentBulletCount--;

            if (_currentBulletCount <= 0)
            {
                gameObject.SetActive(false);
                BulletsExhausted();
            }
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

public class ShooterTargetData
{
    private readonly List<int> _checkedColsForBottom = new();
    private readonly List<int> _checkedColsForTop = new();
    private readonly List<int> _checkedRowsForRight = new();
    private readonly List<int> _checkedRowsForLeft = new();

    public void Reset()
    {
        _checkedColsForBottom.Clear();
        _checkedRowsForRight.Clear();
        _checkedColsForTop.Clear();
        _checkedRowsForLeft.Clear();
    }

    public bool CheckForData(Side side, Vector2Int coords)
    {
        return side switch
        {
            Side.Bottom => !_checkedColsForBottom.Contains(coords.x),
            Side.Right => !_checkedRowsForRight.Contains(coords.y),
            Side.Top => !_checkedColsForTop.Contains(coords.x),
            Side.Left => !_checkedRowsForLeft.Contains(coords.y),
            _ => false
        };
    }

    public void AddTargetData(Side side, Vector2Int coords)
    {
        switch (side)
        {
            case Side.Bottom:
                _checkedColsForBottom.Add(coords.x);
                break;
            case Side.Right:
                _checkedRowsForRight.Add(coords.y);
                break;
            case Side.Top:
                _checkedColsForTop.Add(coords.x);
                break;
            case Side.Left:
                _checkedRowsForLeft.Add(coords.y);
                break;
        }
    }
}