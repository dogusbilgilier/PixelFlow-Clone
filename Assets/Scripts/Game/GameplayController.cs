using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game
{
    public class GameplayController : MonoBehaviour
    {
        [Title("References")]
        [SerializeField] private MainConveyor _mainConveyor;

        private ShooterLaneController _shooterLaneController;
        public bool IsInitialized { get; private set; }

        public void Initialize()
        {
            _mainConveyor.Initialize();
            _shooterLaneController = new ShooterLaneController(3);
            IsInitialized = true;
        }
    }
}

