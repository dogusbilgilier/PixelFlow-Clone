using System;
using Dreamteck;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game
{
    public class GameManager : Singleton<GameManager>
    {
        [Title("References")] [SerializeField] private GameplayController _gameplayController;
        public GameplayController GameplayController => _gameplayController;

        private void Start()
        {
            
        }
    }
}