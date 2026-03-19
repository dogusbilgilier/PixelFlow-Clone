using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace Game
{
    [Serializable]
    public class ShooterLane : IDisposable
    {
        private List<Shooter> _shooters;
        private GameGrid _shooterGrid;

        private int _currentIndex;
        public int TotalShooterCount => _shooters.Count;
        public int RemainingShooterCount => TotalShooterCount - 1 - _currentIndex;
        private int _laneIndex;
        public bool IsInitialized { get; private set; }
        public bool IsLaneCompleted { get; private set; }

        public event Action<ShooterLane> OnLaneCompleted;

        public ShooterLane(List<Shooter> shooters, GameGrid shooterGrid, int laneIndex)
        {
            _shooters = shooters;
            _shooterGrid = shooterGrid;
            _laneIndex = laneIndex;
            _currentIndex = 0;
        }

        public void Dispose()
        {
            OnLaneCompleted = null;
        }

        public ShooterLane(GameGrid shooterGrid, int laneIndex)
        {
            _shooterGrid = shooterGrid;
            _laneIndex = laneIndex;
            _currentIndex = 0;
        }

        public void Initialize()
        {
            _shooters[_currentIndex].SetInFirst();
            IsInitialized = true;
        }

        public void AddShooter(Shooter shooter)
        {
            _shooters ??= new List<Shooter>();

            if (_shooters.Contains(shooter))
                return;

            _shooters.Add(shooter);
        }

        public bool TryGetCurrentShooter(out Shooter shooter)
        {
            shooter = null;

            if (RemainingShooterCount > 0)
                shooter = _shooters[_currentIndex];

            return shooter != null;
        }

        public bool TryGetNextShooter(out Shooter shooter)
        {
            shooter = null;

            if (RemainingShooterCount > 1)
                shooter = _shooters[_currentIndex + 1];

            return shooter != null;
        }

        public int GetPositionInLane(Shooter shooter)
        {
            for (int i = _currentIndex; i < _shooters.Count; i++)
            {
                if (_shooters[i] == shooter)
                    return i - _currentIndex;
            }

            return -1;
        }

        public void OnShooterLeaveTheLane()
        {
            _currentIndex++;
            ArrangeLane();
        }

        private void ArrangeLane()
        {
            int index = 0;
            for (int i = _currentIndex; i < _shooters.Count; i++)
            {
                if (GridHelper.TryGetPositionFromCoords(_shooterGrid, new Vector2Int(_laneIndex, index), out var position))
                {
                    _shooters[i].transform.DOMove(position, 0.2f);
                    index++;
                }
            }

            if (_currentIndex >= _shooters.Count)
            {
                Debug.Log("Lane " + _laneIndex + " is Completed");
                IsLaneCompleted = true;
                OnLaneCompleted?.Invoke(this);
                return;
            }

            _shooters[_currentIndex].SetInFirst();
        }
    }
}