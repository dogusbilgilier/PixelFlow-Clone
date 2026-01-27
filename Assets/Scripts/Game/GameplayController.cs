using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game
{
    public class GameplayController : MonoBehaviour
    {
        [Title("References")]
        [SerializeField] private MainConveyor _mainConveyor;
        [SerializeField] private LevelManager _levelManager;
        [SerializeField] private Shooter _shooterPrefab;
        
        [SerializeField] private ShooterLaneController _shooterLaneController;
        [SerializeField] private TargetObjectController _targetObjectController;
        
        public bool IsInitialized { get; private set; }

        public void Initialize()
        {
            _mainConveyor.Initialize();
            Debug.Assert(_mainConveyor.Bounds.HasValue, " Main Conveyor Doesn't Have Bounds");

            _levelManager.Initialize(_mainConveyor.Bounds.Value);
            _shooterLaneController.Initialize(_mainConveyor.Bounds.Value);
            _targetObjectController.Initialize(_mainConveyor.Bounds.Value);
            IsInitialized = true;
        }
    }
}