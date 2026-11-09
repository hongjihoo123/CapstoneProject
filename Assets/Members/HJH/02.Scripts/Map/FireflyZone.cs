using UnityEngine;

namespace HJH.MapFx
{
    // 반딧불이 구역: 파티클(반딧불이 + 큰 번짐) + 약한 점광원 1개. 비용을 낮추려고
    //  - 카메라에서 멀면(cullDistance) 파티클과 라이트를 통째로 멈춘다 (0.5초마다 한 번만 검사)
    //  - 라이트는 Perlin 노이즈로 은은하게 깜빡인다 (그림자 없음, 범위 작음)
    //  - lightEnabled 를 끄면 라이트 없이 파티클만 쓴다 (성능이 부족할 때)
    public class FireflyZone : MonoBehaviour
    {
        [SerializeField] private Light glowLight;
        [SerializeField] private ParticleSystem[] systems;
        [SerializeField] private float baseIntensity = 1.6f;
        [SerializeField] private float flickerSpeed = 0.7f;
        [SerializeField, Range(0f, 0.9f)] private float flickerAmount = 0.35f;
        [SerializeField] private float cullDistance = 42f;
        [SerializeField] private bool lightEnabled = true;

        private float _seed;
        private float _nextCheck;
        private bool _visible = true;

        private void Awake() => _seed = Random.value * 100f;

        private void Update()
        {
            if (Time.time >= _nextCheck)
            {
                _nextCheck = Time.time + 0.5f + Random.value * 0.2f;
                var cam = Camera.main;
                if (cam != null)
                {
                    bool v = (cam.transform.position - transform.position).sqrMagnitude < cullDistance * cullDistance;
                    if (v != _visible)
                    {
                        _visible = v;
                        foreach (var ps in systems)
                        {
                            if (ps == null) continue;
                            if (v) ps.Play(true); else ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                        }
                    }
                }
            }

            if (glowLight == null) return;
            bool on = _visible && lightEnabled;
            if (glowLight.enabled != on) glowLight.enabled = on;
            if (on)
            {
                float n = Mathf.PerlinNoise(_seed, Time.time * flickerSpeed);
                glowLight.intensity = baseIntensity * (1f - flickerAmount + n * flickerAmount * 2f);
            }
        }
    }
}
