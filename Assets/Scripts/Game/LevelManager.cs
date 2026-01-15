using UnityEngine;

namespace Game
{
    public class LevelManager : MonoBehaviour
    {
        public bool IsInitialized { get; private set; }


        private void Initialize()
        {
            IsInitialized = true;
        }
    }
}