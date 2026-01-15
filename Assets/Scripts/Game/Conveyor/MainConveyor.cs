using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using Dreamteck.Splines;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game
{
    public class MainConveyor : MonoBehaviour
    {
        [Title("References")]
        [SerializeField] private ConveyorFollowerBoard _followerBoardPrefab;

        [SerializeField] private Transform _followerBoardParent;
        [SerializeField] private SplineComputer _spline;
        public SplineComputer Spline => _spline;
        private readonly Queue<ConveyorFollowerBoard> _boardsList = new Queue<ConveyorFollowerBoard>();

        public bool IsInitialized { get; private set; }

        private float _minClickInterval = 0.2f;
        private float _lastClickTime;

        public void Initialize()
        {
            CreateBoards();

            IsInitialized = true;
        }

        private void CreateBoards()
        {
            int count = GameConfigs.Instance.conveyorBoardCount;

            for (int i = 0; i < count; i++)
            {
                var board = Instantiate(_followerBoardPrefab, _followerBoardParent);
                board.Initialize(_spline);
                board.OnBoardCompletedPath += Board_OnOnBoardCompletedPath;

                _boardsList.Enqueue(board);
            }

            ArrangeBoardsInMachine();
        }


        //TODO 
        private void Update()
        {
            if (Input.GetMouseButtonDown(0))
                BoardToConveyor();
        }

        private void BoardToConveyor()
        {
            if (Time.time - _lastClickTime <= _minClickInterval)
                return;

            _lastClickTime = Time.time;

            if (TryGetAvailableBoard(out var board))
            {
                _boardsList.Dequeue();
                board.JumpToConveyorAndMove();
                ArrangeBoardsInMachine();
            }
        }

        private void ArrangeBoardsInMachine()
        {
            float gapBetweenBoards = GameConfigs.Instance.gapBetweenBoards;

            int placementIndex = 0;
            foreach (var board in _boardsList)
            {
                if (board.IsBoardReadyForConveyor || board.IsBoardCompletedPath)
                {
                    string tweenID = $"{board.gameObject.GetInstanceID()}_BoardMoveTweenID";
                    float duration = GameConfigs.Instance.boardConveyorToMachineTweenDuration;
                    Vector3 targetLocalPosition = Vector3.left * gapBetweenBoards * placementIndex;

                    DOTween.Kill(tweenID);
                    board.transform.DOLocalMove(targetLocalPosition, duration).SetId(tweenID);

                    board.PlaceBoardToMachine(targetLocalPosition);
                    placementIndex++;
                }
            }
        }

        private bool TryGetAvailableBoard(out ConveyorFollowerBoard board)
        {
            board = null;

            if (_boardsList.Count > 0 && _boardsList.TryPeek(out var boardToPlace) && boardToPlace.IsBoardReadyForConveyor)
            {
                board = boardToPlace;
                return true;
            }

            return false;
        }

        private void Board_OnOnBoardCompletedPath(ConveyorFollowerBoard board)
        {
            _boardsList.Enqueue(board);
            ArrangeBoardsInMachine();
        }
    }
}