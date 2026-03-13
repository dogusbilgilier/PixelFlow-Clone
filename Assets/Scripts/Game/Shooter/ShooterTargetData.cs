using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    public class ShooterTargetData
    {
        private readonly List<int> _checkedColsForBottom = new();
        private readonly List<int> _checkedColsForTop = new();
        private readonly List<int> _checkedRowsForRight = new();
        private readonly List<int> _checkedRowsForLeft = new();

        public Vector3? LastCheckPosition;

        public void Reset()
        {
            LastCheckPosition = null;
            _checkedColsForBottom.Clear();
            _checkedRowsForRight.Clear();
            _checkedColsForTop.Clear();
            _checkedRowsForLeft.Clear();
        }

        public bool CheckForData(Side side, Vector2Int coords)
        {
            return side switch
            {
                Side.Bottom => !_checkedColsForBottom.Contains(coords.x),
                Side.Right => !_checkedRowsForRight.Contains(coords.y),
                Side.Top => !_checkedColsForTop.Contains(coords.x),
                Side.Left => !_checkedRowsForLeft.Contains(coords.y),
                _ => false
            };
        }

        public void UpdateCheckPosition(Vector3 position)
        {
            LastCheckPosition = position;
        }

        public void AddTargetData(Side side, Vector2Int coords)
        {
            switch (side)
            {
                case Side.Bottom:
                    _checkedColsForBottom.Add(coords.x);
                    break;
                case Side.Right:
                    _checkedRowsForRight.Add(coords.y);
                    break;
                case Side.Top:
                    _checkedColsForTop.Add(coords.x);
                    break;
                case Side.Left:
                    _checkedRowsForLeft.Add(coords.y);
                    break;
            }
        }
    }
}