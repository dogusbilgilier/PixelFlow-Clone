using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game
{
    public class ShooterLaneController : IDisposable
    {
        public bool IsInitialized { get; private set; }

        private readonly List<ShooterLane> _shooterLanes = new List<ShooterLane>();
        private readonly List<Shooter> _allShooters;

        private GameGrid _shooterGrid;
        private ShooterController _shooterController;

        public event Action OnAllLanesCompleted;

        public ShooterLaneController(List<Shooter> allShooters, GameGrid shooterGrid, ShooterController shooterController)
        {
            _allShooters = allShooters;
            _shooterController = shooterController;
            _shooterGrid = shooterGrid;
        }

        public void Dispose()
        {
            OnAllLanesCompleted = null;
        }

        public void Initialize()
        {
            CreateShooterLanes(_allShooters);
            SetShootersCanJumpVisuals();
            IsInitialized = true;
        }
        
        private void CreateShooterLanes(List<Shooter> shooters)
        {
            foreach (ShooterLane lane in _shooterLanes)
                lane.Dispose();
            
            _shooterLanes.Clear();
            
            for (int i = 0; i < LevelManager.Instance.CurrentLevelData.shooterLaneCount; i++)
            {
                ShooterLane lane = new ShooterLane(_shooterGrid, i);
                lane.OnLaneCompleted += Lane_OnLaneCompleted;
                _shooterLanes.Add(lane);
            }

            foreach (var shooter in shooters)
                _shooterLanes[shooter.Data.Coordinates.x].AddShooter(shooter);

            foreach (ShooterLane lane in _shooterLanes)
                lane.Initialize();
        }

        private void Lane_OnLaneCompleted(ShooterLane lane)
        {
            foreach (var shooterLane in _shooterLanes)
            {
                if (!shooterLane.IsLaneCompleted)
                    return;
            }

            OnAllLanesCompleted?.Invoke();
        }

        private void SetShootersCanJumpVisuals()
        {
            foreach (ShooterLane shooterLane in _shooterLanes)
            {
                // Always reset behind shooter first (clear stale state from previous linked jumps)
                if (shooterLane.TryGetNextShooter(out Shooter behindShooter))
                    behindShooter.SetCanJump(false);

                if (!shooterLane.TryGetCurrentShooter(out Shooter frontShooter))
                    continue;

                bool canJump = _shooterController.CheckShooterCanJump(frontShooter, out _);
                frontShooter.SetCanJump(canJump);

                // If front shooter is linked and jumpable with linked,
                // also update the linked shooter behind it
                if (canJump && frontShooter.IsLinked && shooterLane.TryGetNextShooter(out Shooter nextShooter))
                {
                    if (nextShooter == frontShooter.LinkedShooter)
                        nextShooter.SetCanJump(true);
                }
            }
        }

        public ShooterLane GetLane(int laneIndex)
        {
            if (laneIndex < 0 || laneIndex >= _shooterLanes.Count)
                return null;

            return _shooterLanes[laneIndex];
        }

        public void ShooterJumpToConveyorFromLane(Shooter shooter)
        {
            _shooterLanes[shooter.Data.Coordinates.x].OnShooterLeaveTheLane();
            SetShootersCanJumpVisuals();
        }
    }
}