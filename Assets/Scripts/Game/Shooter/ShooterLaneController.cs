using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game
{
    public class ShooterLaneController : MonoBehaviour
    {
        [Title("References")]
        [SerializeField] private Shooter _shooterPrefab;
        [SerializeField] private Transform _shooterParent;

        private int _totalLaneCount;
        private List<ShooterLane> _shooterLanes = new List<ShooterLane>();
        private Bounds _mainConveyorBounds;
        private GameGrid _shooterAreaGrid;

        public bool IsInitialized { get; private set; }

        public void Initialize(Bounds mainConveyorBounds)
        {
            _mainConveyorBounds = mainConveyorBounds;
            _totalLaneCount = LevelManager.Instance.CurrentLevelData.shooterLaneCount;
            _shooterAreaGrid = GridHelper.CreateShooterGrid(LevelManager.Instance.CurrentLevelData,_mainConveyorBounds.min.z);
            
            CreateShooterLanes();
            IsInitialized = true;
        }
        
        private void CreateShooterLanes()
        {
            for (int i = 0; i < _totalLaneCount; i++)
            {
                ShooterLaneData laneData = LevelManager.Instance.CurrentLevelData.shooterLaneDataList[i];
                List<Shooter> shooters = new List<Shooter>();

                foreach (var shooterData in laneData.ShooterDataList)
                    shooters.Add(CreateShooter(shooterData));

                ShooterLane lane = new ShooterLane(shooters);
                _shooterLanes.Add(lane);
            }
        }

        private Shooter CreateShooter(ShooterData shooterData)
        {
            if (GridHelper.TryGetPositionFromCoords(_shooterAreaGrid, shooterData.Coordinates, out Vector3 position))
            {
                Shooter shooter = Instantiate(_shooterPrefab, _shooterParent);
                shooter.transform.position = position;
                shooter.Initialize(shooterData);
                return shooter;
            }

            Debug.LogError("Shooter coordinates is out of grid!");
            return null;
        }
    }
}