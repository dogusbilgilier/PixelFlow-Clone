using System;
using UnityEngine;

namespace Game
{
    public class StoragePiece : GridPiece
    {
        public Shooter AssignedShooter { get; private set; }


        public void Assign(Shooter shooter)
        {
            AssignedShooter = shooter;
        }

        private void OnDestroy()
        {
            AssignedShooter = null;
        }

        public void Unassign()
        {
            if (AssignedShooter == null)
                return;

            AssignedShooter = null;
        }
    }
}