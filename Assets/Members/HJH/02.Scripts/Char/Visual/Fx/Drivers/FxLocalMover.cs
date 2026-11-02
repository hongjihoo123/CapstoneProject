using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Visual
{
    // 이펙트가 켜질 때마다 target 을 from → to (로컬 좌표) 로 duration 동안 옮김. 점점 빨라짐 (떨어지는 운석)
    // target 의 자식(바위, 불꽃 꼬리, 공기 막)이 같이 따라 움직임. 꼬리는 월드 공간 + 거리당 방출로 만들면 됨
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

            // 풀에서 재사용될 때 끝자리 → 시작자리로 순간이동한 거리만큼 꼬리가 그어지지 않게 다시 시작
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

        // 켜지고 elapsed 초 지난 자리로 (영상 녹화처럼 시간을 직접 넘길 때도 씀)
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
