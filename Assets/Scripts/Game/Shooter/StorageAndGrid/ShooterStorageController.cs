using UnityEngine;
using UnityEngine.Pool;

namespace Game
{
    public class ShooterStorageController : MonoBehaviour
    {
        public bool IsInitialized { get; private set; }
        public bool IsPrepared { get; private set; }
        private StoragePiece[] _storageVisualPieces;

        public void Initialize()
        {
            
            IsInitialized = true;
        }

        public void Prepare(StoragePiece[] storageVisualPieces)
        {
            _storageVisualPieces = storageVisualPieces;
            IsPrepared = true;
        }

        public bool TryConsumeShooter(Shooter shooter)
        {
            foreach (StoragePiece storage in _storageVisualPieces)
            {
                if (storage.AssignedShooter != null)
                    continue;

                shooter.JumpToStorage(storage);
                storage.Assign(shooter);

                return true;
            }

            return false;
        }

        public void ReleaseShooter(Shooter shooter)
        {
            foreach (StoragePiece storage in _storageVisualPieces)
            {
                if (storage.AssignedShooter == shooter)
                {
                    storage.Unassign();
                    break;
                }
            }
        }

        public bool IsShooterInStorage(Shooter shooter, out StoragePiece storagePiece)
        {
            storagePiece = null;
            foreach (StoragePiece storage in _storageVisualPieces)
            {
                if (storage.AssignedShooter == shooter)
                {
                    storagePiece = storage;
                    return true;
                }
            }

            return false;
        }

        public void ArrangeStorageShooters()
        {
            var shootersInStorage = ListPool<Shooter>.Get();

            foreach (StoragePiece storage in _storageVisualPieces)
            {
                if (storage.AssignedShooter != null)
                {
                    shootersInStorage.Add(storage.AssignedShooter);
                    storage.Unassign();
                }
            }

            for (var i = 0; i < shootersInStorage.Count; i++)
            {
                var shooter = shootersInStorage[i];
                var storagePiece = _storageVisualPieces[i];
                storagePiece.Assign(shooter);
                shooter.transform.SetParent(storagePiece.transform);
                shooter.transform.localPosition = Vector3.zero;
            }


            ListPool<Shooter>.Release(shootersInStorage);
        }
    }
}