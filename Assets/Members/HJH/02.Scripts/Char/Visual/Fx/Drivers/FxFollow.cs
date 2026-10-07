using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Visual
{
    // Keeps the effect on a moving target (auras, dash trail). Not "playing" by itself: lifetime is up to the handle.
    public class FxFollow : MonoBehaviour, IFxDriver
    {
        private Transform _target;
        private Vector3 _offset;

        public bool IsPlaying => false;

        public void Play(in FxParams parameters)
        {
            _target = parameters.Follow;
            _offset = parameters.Offset;
            LateUpdate();
        }

        public void Stop() => _target = null;

        private void LateUpdate()
        {
            if (_target != null)
                transform.position = _target.position + _offset;
        }
    }
}
