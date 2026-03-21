using Sirenix.OdinInspector;
using UnityEngine;

namespace Game
{
    public class LevelCreator : MonoBehaviour
    {
        [Title("References")]
        [SerializeField] private LevelData _levelData;

        public LevelData LevelData => _levelData;
        public Shooter shooterPrefab;
        public TargetObject targetObjectPrefab;
        public Transform shooterParent;
        public Transform targetObjectParent;
        public MainConveyor mainConveyorPrefab;
    }
}