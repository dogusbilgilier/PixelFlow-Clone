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

        public void AssignPool(IObjectPool<Bullet> pool)
        {
            _pool = pool;
        }

        private void Release()
        {
            _pool.Release(this);
            OnReachToTarget = null;
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