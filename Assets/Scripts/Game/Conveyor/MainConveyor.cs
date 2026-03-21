using System.Collections.Generic;
using Dreamteck.Splines;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace Game
{
    public class MainConveyor : MonoBehaviour
    {
        [Title("References")]
        [SerializeField] private ConveyorFollowerBoard _followerBoardPrefab;
        [SerializeField] private TextMeshPro _boardCountIndicator;
        [SerializeField] private Transform _followerBoardParent;
        [SerializeField] private SplineComputer _spline;
        public SplineComputer Spline => _spline;
        private readonly Queue<ConveyorFollowerBoard> _boardQueue = new Queue<ConveyorFollowerBoard>();
        private List<ConveyorFollowerBoard> _allBoards = new List<ConveyorFollowerBoard>();

        public bool IsInitialized { get; private set; }
        public bool IsPrepared { get; private set; }

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
            IsInitialized = true;
        }

        public void Prepare()
        {
            IsPrepared = false;
            _boardQueue.Clear();
            

            for (int i = _allBoards.Count - 1; i >= 0; i--)
            {
                ConveyorFollowerBoard board = _allBoards[i];
                
                if (board.AssignedShooter != null)
                    board.AssignedShooter.ResetParent();
                
                DestroyImmediate(board.gameObject);
            }

            _allBoards.Clear();

            CreateBoards();

            foreach (ConveyorFollowerBoard board in _allBoards)
            {
                if (!board.IsBoardReadyForConveyor || !board.IsBoardCompletedPath)
                    board.CompletePath();

                _boardQueue.Enqueue(board);
            }

            ArrangeBoardsInMachine();
            IsPrepared = true;
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

                _allBoards.Add(board);
            }

            ArrangeBoardsInMachine();
        }

        public void BoardToConveyor(ConveyorFollowerBoard board)
        {
            _boardQueue.Dequeue();
            board.JumpToConveyor();
            ArrangeBoardsInMachine();
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

            _boardCountIndicator.SetText($"{_boardQueue.Count}/{_allBoards.Count}");
        }

        public void ShooterDestroyed(Shooter shooter)
        {
            foreach (var board in _allBoards)
            {
                if (board.AssignedShooter == null || board.AssignedShooter != shooter)
                    continue;

                board.OnShooterExhausted();
                return;
            }
        }

        public bool TryGetAvailableBoard(out ConveyorFollowerBoard board)
        {
            board = null;

            if (_boardQueue.Count <= 0 || !_boardQueue.TryPeek(out var boardToPlace) || !boardToPlace.IsBoardReadyForConveyor)
                return false;

            board = boardToPlace;
            return true;
        }

        private void Board_OnArrangeBoardsRequested(ConveyorFollowerBoard board)
        {
            _boardQueue.Enqueue(board);
            ArrangeBoardsInMachine();
        }

        private void Board_OnOnBoardCompletedPath(ConveyorFollowerBoard board)
        {
            _boardQueue.Enqueue(board);
            ArrangeBoardsInMachine();
        }
    }
}