using System.Collections.Generic;
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
        private readonly Queue<ConveyorFollowerBoard> _boardQueue = new Queue<ConveyorFollowerBoard>();
        private List<ConveyorFollowerBoard> _allBoards = new List<ConveyorFollowerBoard>();

        public bool IsInitialized { get; private set; }

        public Bounds? Bounds
        {
            get
            {
                if (Spline.TryGetComponent(out Renderer splineRenderer))
                    return splineRenderer.bounds;

                return null;
            }
        }

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
                board.OnArrangeBoardsRequested += Board_OnArrangeBoardsRequested;

                _boardQueue.Enqueue(board);
                _allBoards.Add(board);
            }

            ArrangeBoardsInMachine();
        }

        public void BoardToConveyor()
        {
            if (TryGetAvailableBoard(out var board))
            {
                _boardQueue.Dequeue();
                board.JumpToConveyorAndMove();
                ArrangeBoardsInMachine();
            }
        }

        private void ArrangeBoardsInMachine()
        {
            int placementIndex = 0;
            foreach (var board in _boardQueue)
            {
                if (board.IsBoardReadyForConveyor || board.IsBoardCompletedPath)
                {
                    board.PlaceBoardToMachine(placementIndex);
                    placementIndex++;
                }
            }
        }

        private void Board_OnArrangeBoardsRequested(ConveyorFollowerBoard board)
        {
            _boardQueue.Enqueue(board);
            ArrangeBoardsInMachine();
        }

        public bool TryGetAvailableBoard(out ConveyorFollowerBoard board)
        {
            board = null;

            if (_boardQueue.Count > 0 && _boardQueue.TryPeek(out var boardToPlace) && boardToPlace.IsBoardReadyForConveyor)
            {
                board = boardToPlace;
                return true;
            }

            return false;
        }

        private void Board_OnOnBoardCompletedPath(ConveyorFollowerBoard board)
        {
            _boardQueue.Enqueue(board);
            ArrangeBoardsInMachine();
        }

        public void ShooterDestroyed(Shooter shooter)
        {
            foreach (var board in _allBoards)
            {
                if (board.AssignedShooter != null && board.AssignedShooter == shooter)
                {
                    board.OnShooterExhausted();
                    return;
                }
            }
        }
    }
}