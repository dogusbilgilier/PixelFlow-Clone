using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game
{
    public class GameplayController : MonoBehaviour
    {
        [Title("References")]
        [SerializeField] private MainConveyor _mainConveyor;

        public bool IsInitialized { get; private set; }

        public void Initialize()
        {
            _mainConveyor.Initialize();
            IsInitialized = true;
        }
    }
}

public enum GameplayState
{
    Menu,
    Gameplay,
    GameplayFinished
}