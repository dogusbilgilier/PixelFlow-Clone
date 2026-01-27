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

        private Vector3 _boardVisualDefaultLocalPos;
        private Sequence _placeBoardToMachineSequence;
        private Sequence _placeBoardToConveyorSequence;

        public event Action<ConveyorFollowerBoard> OnBoardCompletedPath;

        public void Initialize(SplineComputer spline)
        {
            _splineFollower = this.gameObject.AddComponent<SplineFollower>();
            _splineFollower.follow = false;
            _splineFollower.spline = spline;
            _splineFollower.followSpeed = 0f;

            _splineFollower.onEndReached += SplineFollower_OnEndReached;
            _boardVisualDefaultLocalPos = _boardVisual.transform.localPosition;

            IsInitialized = true;
        }

        private void OnDestroy()
        {
            OnBoardCompletedPath = null;
            _splineFollower.onEndReached -= SplineFollower_OnEndReached;
        }

        public void PlaceBoardToMachine(Vector3 targetLocalPos)
        {
            float duration = GameConfigs.Instance.boardConveyorToMachineTweenDuration;
            _placeBoardToMachineSequence?.Kill(false);
            _placeBoardToConveyorSequence?.Kill(false);

            _placeBoardToMachineSequence = DOTween.Sequence();
            _placeBoardToMachineSequence.Insert(0f, transform.DOLocalMove(targetLocalPos, duration));
            _placeBoardToMachineSequence.Insert(0f, transform.DOLocalRotate(Vector3.up * 90, duration));
            _placeBoardToMachineSequence.Insert(0f, _boardVisual.transform.DOLocalMove(_boardVisualDefaultLocalPos, duration));
            _placeBoardToMachineSequence.Insert(0f, _boardVisual.transform.DOLocalRotate(Vector3.zero, duration));
            _placeBoardToMachineSequence.OnComplete(() => { IsBoardReadyForConveyor = true; });
        }

        public void JumpToConveyorAndMove()
        {
            IsBoardReadyForConveyor = false;
            IsBoardCompletedPath = false;

            _splineFollower.SetPercent(0);
            _splineFollower.follow = true;
            _splineFollower.followSpeed = 10;

            float duration = GameConfigs.Instance.boardMachineToConveyorTweenDuration;
            _placeBoardToMachineSequence?.Kill(false);
            _placeBoardToConveyorSequence?.Kill(false);

            _placeBoardToConveyorSequence = DOTween.Sequence();
            _placeBoardToConveyorSequence.Insert(0, _boardVisual.transform.DOLocalRotate(new Vector3(-90, 0, 0), duration, RotateMode.LocalAxisAdd));
            _placeBoardToConveyorSequence.Insert(0, _boardVisual.transform.DOLocalMove(Vector3.zero, duration));
        }

        private void SplineFollower_OnEndReached(double obj)
        {
            IsBoardCompletedPath = true;
            _splineFollower.follow = false;
            OnBoardCompletedPath?.Invoke(this);
        }
    }
}