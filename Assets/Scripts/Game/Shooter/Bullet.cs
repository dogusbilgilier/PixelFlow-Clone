using System;
using DG.Tweening;
using Dreamteck.Splines.Primitives;
using UnityEngine;
using UnityEngine.Pool;

namespace Game
{
    public class Bullet : MonoBehaviour
    {
        private IObjectPool<Bullet> _pool;
        public event Action<Bullet, TargetObject> OnReachToTarget;
        private bool _isActive;

        public void AssignPool(IObjectPool<Bullet> pool)
        {
            _pool = pool;
        }

        public void ForceRelease()
        {
            DOTween.Kill(this.transform);
            OnReachToTarget = null;
            if (_isActive)
                _pool.Release(this);
        }

        public void SetActive(bool isActive)
        {
            _isActive = isActive;
        }

        private void Release()
        {
            DOTween.Kill(this.transform);
            OnReachToTarget = null;
            _pool.Release(this);
        }

        public void MoveTo(TargetObject targetObject)
        {
            float speed = GameConfigs.Instance.shooterBulletSpeed;
            transform.DOMove(targetObject.transform.position, speed).SetEase(Ease.Linear).SetSpeedBased(true).OnComplete(() =>
            {
                OnReachToTarget?.Invoke(this, targetObject);
                Release();
            });
        }
    }
}