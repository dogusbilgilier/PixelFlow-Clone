using System.Collections.Generic;

namespace Game
{
    public class ShooterLaneController
    {
        private int _totalLaneCount;
        private List<ShooterLane> _shooterLanes;

        public ShooterLaneController(int totalLaneCount)
        {
            _totalLaneCount = totalLaneCount;
        }
    }
}