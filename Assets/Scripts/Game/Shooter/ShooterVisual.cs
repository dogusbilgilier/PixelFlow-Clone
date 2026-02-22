using UnityEngine;

namespace Game
{
    public class ShooterVisual : MonoBehaviour
    {
        public bool IsInitialized { get; private set; }


        public void Initialize()
        {
            IsInitialized = true;
        }
    }
}