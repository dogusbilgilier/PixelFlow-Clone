using System.Collections.Generic;

namespace Game
{
    public class ShooterLane
    {
        private List<Shooter> _shooters;
        private int _currentIndex;
        public int TotalShooterCount => _shooters.Count;
        public int RemainingShooterCount => TotalShooterCount - 1 - _currentIndex;

        public ShooterLane(List<Shooter> shooters)
        {
            _shooters = shooters;
        }

        public bool TryGetCurrentShooter(out Shooter shooter)
        {
            shooter = null;

            if (RemainingShooterCount > 0)
                shooter = _shooters[_currentIndex];

            return shooter != null;
        }
    }
}