using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game
{
    public class ShooterLaneController
    {
        public bool IsInitialized { get; private set; }
        
        private readonly List<ShooterLane> _shooterLanes = new List<ShooterLane>();
        private readonly List<Shooter> _allShooters;
        
        private GameGrid _shooterGrid;
        private ShooterController _shooterController;
        
        public ShooterLaneController(List<Shooter> allShooters, GameGrid shooterGrid, ShooterController shooterController)
        {
            _allShooters = allShooters;
            _shooterController = shooterController;
            _shooterGrid = shooterGrid;
        }

        public void Initialize()
        {
            CreateShooterLanes(_allShooters);
            SetShootersCanJumpVisuals();
            IsInitialized = true;
        }

        private void CreateShooterLanes(List<Shooter> shooters)
        {
            for (int i = 0; i < LevelManager.Instance.CurrentLevelData.shooterLaneCount; i++)
                _shooterLanes.Add(new ShooterLane(_shooterGrid, i));

            foreach (var shooter in shooters)
                _shooterLanes[shooter.Data.Coordinates.x].AddShooter(shooter);

            foreach (ShooterLane lane in _shooterLanes)
                lane.Initialize();
        }

        private void SetShootersCanJumpVisuals()
        {
            foreach (ShooterLane shooterLane in _shooterLanes)
            {
                // Reset front shooter
                if (shooterLane.TryGetCurrentShooter(out Shooter frontShooter))
                {
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

                // Reset the next shooter if it's not linked-jumpable
                if (shooterLane.TryGetNextShooter(out Shooter behindShooter))
                {
                    if (!behindShooter.CanJump)
                        behindShooter.SetCanJump(false);
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