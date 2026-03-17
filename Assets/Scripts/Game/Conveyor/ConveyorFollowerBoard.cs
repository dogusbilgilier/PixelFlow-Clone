using System;
using DG.Tweening;
using Dreamteck.Splines;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game
{
    public class ConveyorFollowerBoard : MonoBehaviour
    {
        [Title("References")]
        [SerializeField] private GameObject _boardVisual;

        private SplineFollower _splineFollower;
        public bool IsInitialized { get; private set; }
        public bool IsBoardReadyForConveyor { get; private set; } = true;
        public bool IsBoardCompletedPath { get; private set; } = true;
        public Shooter AssignedShooter { get; private set; }

        private Sequence _placeBoardToMachineSequence;
        private Sequence _placeBoardToConveyorSequence;
        private Tweener _startMoveTween;
        private float _followSpeed;
        public event Action<ConveyorFollowerBoard> OnBoardCompletedPath;
        public event Action<ConveyorFollowerBoard> OnArrangeBoardsRequested;

        public void Initialize(SplineComputer spline)
        {
            _splineFollower = this.gameObject.AddComponent<SplineFollower>();
            _splineFollower.follow = false;
            _splineFollower.spline = spline;
            _splineFollower.followSpeed = 0f;
            _followSpeed = GameConfigs.Instance.boardFollowSpeed;
            _splineFollower.onEndReached += SplineFollower_OnEndReached;
            IsInitialized = true;
        }

        private void SplineFollower_OnEndReached(double obj)
        {
            if (IsBoardReadyForConveyor || IsBoardCompletedPath)
                return;
            OnCompletePath();
        }

        private void OnDestroy()
        {
            OnBoardCompletedPath = null;
            OnArrangeBoardsRequested = null;
            _startMoveTween?.Kill(false);
            _placeBoardToMachineSequence?.Kill(false);
            _placeBoardToConveyorSequence?.Kill(false);
        }

        public void PlaceBoardToMachine(int index)
        {
            float duration = GameConfigs.Instance.boardConveyorToMachineTweenDuration;
            float gapBetweenBoards = GameConfigs.Instance.gapBetweenBoards;

            _placeBoardToMachineSequence?.Kill(false);
            _placeBoardToConveyorSequence?.Kill(false);

            _placeBoardToMachineSequence = DOTween.Sequence();

            _placeBoardToMachineSequence.Insert(0f, transform.DOLocalMove(new Vector3(-(index * gapBetweenBoards), 0f, 0f), duration));
            _placeBoardToMachineSequence.Insert(0f, transform.DOLocalRotate(new Vector3(0, 90, 0), duration));
            _placeBoardToMachineSequence.Insert(0f, _boardVisual.transform.DOLocalMoveY(0.75f, duration));
            _placeBoardToMachineSequence.Insert(0f, _boardVisual.transform.DOLocalRotateQuaternion(Quaternion.identity, duration));

            _placeBoardToMachineSequence.OnComplete(() => { IsBoardReadyForConveyor = true; });
        }

        public void JumpToConveyor()
        {
            Vector3 splineStartPos = _splineFollower.EvaluatePosition(0.0f);
            float duration = GameConfigs.Instance.boardMachineToConveyorTweenDuration;

            _startMoveTween?.Kill(false);
            _placeBoardToMachineSequence?.Kill(false);
            _placeBoardToConveyorSequence?.Kill(false);

            _placeBoardToConveyorSequence = DOTween.Sequence();
            _placeBoardToConveyorSequence.Insert(0f, transform.DOLocalRotate(new Vector3(0, 90, 0), duration));
            _placeBoardToConveyorSequence.Insert(0, _boardVisual.transform.DOLocalRotate(new Vector3(-90, 0, 0), duration * 0.75f, RotateMode.LocalAxisAdd));
            _placeBoardToConveyorSequence.Insert(0, _boardVisual.transform.DOLocalMove(Vector3.zero, duration));

            _startMoveTween = transform.DOMove(splineStartPos, duration).SetEase(Ease.Linear);

            _startMoveTween.OnComplete(() =>
            {
                _splineFollower.SetPercent(0.0);
                IsBoardReadyForConveyor = false;
                IsBoardCompletedPath = false;
                _splineFollower.follow = true;
            });
        }

        public void StartMove()
        {
            _splineFollower.followSpeed = _followSpeed;
        }

        private void OnCompletePath()
        {
            ResetBoard();
            OnBoardCompletedPath?.Invoke(this);
        }

        public void SetAssignedShooter(Shooter shooter)
        {
            AssignedShooter = shooter;
        }

        public void OnShooterExhausted()
        {
            ResetBoard();
            OnArrangeBoardsRequested?.Invoke(this);
        }

        private void ResetBoard()
        {
            AssignedShooter = null;
            IsBoardCompletedPath = true;
            _splineFollower.follow = false;
            _splineFollower.followSpeed = 0f;
        }
    }
}