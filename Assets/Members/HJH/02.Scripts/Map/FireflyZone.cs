using UnityEngine;

namespace HJH.MapFx
{
    public class FireflyZone : MonoBehaviour
    {
        [SerializeField] private Light glowLight;
        [SerializeField] private ParticleSystem[] systems;
        [SerializeField] private float baseIntensity = 1.6f;
        [SerializeField] private float flickerSpeed = 0.7f;
        [SerializeField, Range(0f, 0.9f)] private float flickerAmount = 0.35f;
        [SerializeField] private float cullDistance = 42f;
        [SerializeField] private bool lightEnabled = true;

        [Header("v2 깜빡임 (입자마다 다른 박자)")]
        [SerializeField] private ParticleSystem flies;
        [SerializeField] private Vector2 onSeconds = new Vector2(0.15f, 0.45f);
        [SerializeField] private Vector2 offSeconds = new Vector2(1.5f, 4f);
        [SerializeField] private Color colorA = new Color(1f, 0.878f, 0.4f);
        [SerializeField] private Color colorB = new Color(0.784f, 1f, 0.353f);
        [SerializeField] private Vector2 brightness = new Vector2(0.45f, 1f);
        [SerializeField, Range(0f, 1f)] private float idleGlow = 0.04f;

        [Header("v2 날아다니기 (입자마다 다른 방향·속도로 천천히 선회, 구역 밖으로 나가면 돌아옴)")]
        [SerializeField] private Vector2 wanderSpeed = new Vector2(0.3f, 0.8f);
        [SerializeField] private float wanderTurn = 0.6f;
        [SerializeField] private float leashRadius = 3.2f;

        [Header("v2 흩어짐 (플레이어 근처)")]
        [SerializeField] private float scatterRadius = 2.2f;
        [SerializeField] private float scatterPush = 2.4f;

        private float _seed;
        private float _nextCheck;
        private bool _visible = true;
        private ParticleSystem.Particle[] _buf;
        private Transform _player;
        private float _nextPlayerFind;

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

            if (_visible && flies != null) UpdateFlies();

            if (glowLight == null) return;
            bool on = _visible && lightEnabled;
            if (glowLight.enabled != on) glowLight.enabled = on;
            if (on)
            {
                float n = Mathf.PerlinNoise(_seed, Time.time * flickerSpeed);
                glowLight.intensity = baseIntensity * (1f - flickerAmount + n * flickerAmount * 2f);
            }
        }

        private void UpdateFlies()
        {
            int max = flies.main.maxParticles;
            if (_buf == null || _buf.Length < max) _buf = new ParticleSystem.Particle[max];
            int n = flies.GetParticles(_buf);
            if (n == 0) return;

            if (_player == null && Time.time >= _nextPlayerFind)
            {
                _nextPlayerFind = Time.time + 2f;
                var p = GameObject.FindGameObjectWithTag("Player");
                if (p != null) _player = p.transform;
            }
            bool scatter = false;
            Vector3 playerLocal = default;
            var ft = flies.transform;
            if (_player != null)
            {
                Vector3 pp = _player.position + Vector3.up * 1f;
                if ((pp - ft.position).sqrMagnitude < (scatterRadius + 4f) * (scatterRadius + 4f))
                {
                    scatter = true;
                    playerLocal = ft.InverseTransformPoint(pp);
                }
            }

            float t = Time.time, dt = Time.deltaTime;
            float r2 = scatterRadius * scatterRadius;
            for (int i = 0; i < n; i++)
            {
                uint s = _buf[i].randomSeed;
                float h0 = Hash(s, 0), h1 = Hash(s, 1), h2 = Hash(s, 2), h3 = Hash(s, 3), h4 = Hash(s, 4);
                float on = Mathf.Lerp(onSeconds.x, onSeconds.y, h0);
                float off = Mathf.Lerp(offSeconds.x, offSeconds.y, h1);
                float period = on + off;
                float phase = (t + h2 * period + Mathf.Sin(t * 0.37f + h3 * 6.28f) * 0.4f) % period;
                float a = 0f;
                if (phase < on)
                {
                    float u = phase / on;
                    a = u < 0.3f ? u / 0.3f : 1f - (u - 0.3f) / 0.7f;
                    a = a * a;
                }
                a = Mathf.Max(a, idleGlow);
                var c = Color.Lerp(colorA, colorB, h3) * Mathf.Lerp(brightness.x, brightness.y, h4);
                c.a = a;
                _buf[i].startColor = c;

                float sp = Mathf.Lerp(wanderSpeed.x, wanderSpeed.y, h4);
                float wt = t * wanderTurn * (0.6f + h1 * 0.8f);
                var want = new Vector3(Mathf.Sin(wt + h2 * 6.28f), 0.35f * Mathf.Sin(wt * 1.7f + h0 * 6.28f), Mathf.Cos(wt * 0.83f + h3 * 6.28f)) * sp;
                Vector3 pos = _buf[i].position;
                Vector3 home = new Vector3(0f, 1.4f, 0f);
                Vector3 away = pos - home; away.y *= 2f;
                float od = away.magnitude;
                if (od > leashRadius) want += -away / od * sp * Mathf.Min((od - leashRadius) * 1.5f, 2f);
                _buf[i].velocity = Vector3.Lerp(_buf[i].velocity, want, Mathf.Min(1f, dt * 1.5f));

                if (scatter)
                {
                    Vector3 d = _buf[i].position - playerLocal;
                    float dd = d.sqrMagnitude;
                    if (dd < r2 && dd > 1e-4f)
                    {
                        float k = 1f - Mathf.Sqrt(dd) / scatterRadius;
                        _buf[i].velocity += d.normalized * (scatterPush * k * dt * 12f);
                    }
                }
            }
            flies.SetParticles(_buf, n);
        }

        private static float Hash(uint s, uint k)
        {
            uint x = s * 747796405u + k * 2891336453u + 0x9E3779B9u;
            x ^= x >> 16; x *= 0x7feb352du; x ^= x >> 15; x *= 0x846ca68bu; x ^= x >> 16;
            return (x & 0xFFFFFF) / 16777215f;
        }
    }
}
