using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Visual
{
    // A held effect (orb, magic circle, aura, aim line...). Move it, feed it values, Kill it when done;
    // it fades out and goes back to the pool by itself. Safe to use when the prefab slot is empty (does nothing).
    public sealed class FxHandle
    {
        private readonly FxPrefabInstance _instance;
        private bool _killed;

        internal FxHandle(FxPrefabInstance instance, Vector3 position)
        {
            _instance = instance;
            Position = position;
        }

        public Vector3 Position { get; private set; }

        public void MoveTo(Vector3 position)
        {
            Position = position;
            if (!_killed && _instance != null)
                _instance.transform.position = position;
        }

        public void SetValue(float value)
        {
            if (!_killed && _instance != null)
                _instance.SetValue(value);
        }

        public void SetSpeed(float value)
        {
            if (!_killed && _instance != null)
                _instance.SetSpeed(value);
        }

        public void SetPoints(Vector3 start, Vector3 end)
        {
            if (!_killed && _instance != null)
                _instance.SetPoints(start, end);
        }

        public void SetTint(Color tint)
        {
            if (!_killed && _instance != null)
                _instance.SetTint(tint);
        }

        public void Kill()
        {
            if (_killed)
                return;

            _killed = true;
            if (_instance != null)
                _instance.Kill();
        }
    }
}
