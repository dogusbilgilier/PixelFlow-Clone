using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game
{
    public class ShooterLaneController
    {
        private List<ShooterLane> _shooterLanes = new List<ShooterLane>();
        private GameGrid _shooterGrid;

        public ShooterLaneController(List<Shooter> allShooters, GameGrid shooterGrid)
        {
            _shooterGrid = shooterGrid;
            CreateShooterLanes(allShooters);
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

        public void ShooterJumpToConveyorFromLane(Shooter shooter)
        {
            _shooterLanes[shooter.Data.Coordinates.x].OnShooterLeaveTheLane();
        }
    }
}