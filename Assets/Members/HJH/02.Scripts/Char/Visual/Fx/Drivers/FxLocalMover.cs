using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Visual
{
    public class FxLocalMover : MonoBehaviour, IFxDriver
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 from = new(-3f, 13f, -2.5f);
        [SerializeField] private Vector3 to = Vector3.zero;
        [SerializeField, Min(0.01f)] private float duration = 0.7f;
        [SerializeField, Tooltip("1 = 일정한 속도, 2 이상 = 끝으로 갈수록 빨라짐")] private float acceleration = 1.6f;

        private float _elapsed = -1f;

        public bool IsPlaying => _elapsed >= 0f && _elapsed < duration;

        public void Configure(Transform moved, Vector3 start, Vector3 end, float seconds, float accel)
        {
            target = moved;
            from = start;
            to = end;
            duration = seconds;
            acceleration = accel;
        }

        public void Play(in FxParams parameters)
        {
            if (target == null)
                return;
            _elapsed = 0f;
            target.localPosition = from;
            Vector3 travel = to - from;
            if (travel.sqrMagnitude > 0.0001f)
                target.localRotation = Quaternion.LookRotation(travel);

            foreach (ParticleSystem ps in target.GetComponentsInChildren<ParticleSystem>())
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.Play(true);
            }
        }

        public void Stop() => _elapsed = -1f;

        private void Update()
        {
            if (target == null || _elapsed < 0f || _elapsed >= duration)
                return;
            _elapsed += Time.deltaTime;
            Evaluate(_elapsed);
        }

        public void Evaluate(float elapsed)
        {
            if (target == null)
                return;
            float t = Mathf.Clamp01(elapsed / duration);
            target.localPosition = Vector3.LerpUnclamped(from, to, Mathf.Pow(t, Mathf.Max(0.1f, acceleration)));
            Vector3 travel = to - from;
            if (travel.sqrMagnitude > 0.0001f)
                target.localRotation = Quaternion.LookRotation(travel);
        }
    }
}
