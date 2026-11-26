using UnityEngine;

namespace Assets.Members.HJH.Environment
{
    public class WaterSplashPool : MonoBehaviour
    {
        [SerializeField] private ParticleSystem splashPrefab;
        [SerializeField] private int poolSize = 8;
        [Tooltip("프리팹이 이 반지름 기준으로 만들어져 있음 → 요청 반지름에 맞춰 크기 조절")]
        [SerializeField] private float baseRadius = 1.5f;

        private ParticleSystem[] _pool;
        private int _next;

        private void Awake()
        {
            if (splashPrefab == null)
            {
                Debug.LogWarning($"{name}: Splash 프리팹이 비어 있어 물보라 없이 물결만 생깁니다.", this);
                _pool = new ParticleSystem[0];
                return;
            }

            _pool = new ParticleSystem[Mathf.Max(1, poolSize)];
            for (int i = 0; i < _pool.Length; i++)
            {
                _pool[i] = Instantiate(splashPrefab, transform);
                KeepAfterPlay(_pool[i]);
            }
        }

        private static void KeepAfterPlay(ParticleSystem root)
        {
            foreach (ParticleSystem system in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = system.main;
                main.stopAction = ParticleSystemStopAction.None;
            }
        }

        private void OnEnable() => JungleWater.Register(this);
        private void OnDisable() => JungleWater.Unregister(this);

        public void Spawn(Vector3 position, float radius)
        {
            if (_pool.Length == 0)
                return;

            ParticleSystem splash = _pool[_next];
            if (splash == null)
            {
                splash = _pool[_next] = Instantiate(splashPrefab, transform);
                KeepAfterPlay(splash);
            }

            _next = (_next + 1) % _pool.Length;

            splash.transform.position = position;
            splash.transform.localScale = Vector3.one * (radius / baseRadius);
            splash.Clear(true);
            splash.Play(true);
        }
    }
}
