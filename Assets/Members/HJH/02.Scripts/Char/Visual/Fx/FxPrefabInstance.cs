using System.Collections.Generic;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Visual
{
    // Root of every effect prefab (added automatically if missing). Handles pooling, tint, and lifetime:
    // one-shots return to the pool by themselves once all particles died and no driver is playing;
    // held effects (handles) do the same after Kill.
    public class FxPrefabInstance : MonoBehaviour
    {
        private static readonly int TintId = Shader.PropertyToID("_Tint");
        private const int DefaultMaxInstances = 12;

        // Per prefab: idle instances ready to reuse, and every instance ever made (for the cap).
        private sealed class Pool
        {
            public readonly Stack<FxPrefabInstance> Idle = new();
            public readonly List<FxPrefabInstance> All = new();
        }

        private static readonly Dictionary<GameObject, Pool> Pools = new();

        [SerializeField, Min(1), Tooltip("Most copies of this effect alive at once. When all are busy, the oldest one is recycled.")]
        private int maxInstances = DefaultMaxInstances;

        [SerializeField, Tooltip("Systems that keep their own color (smoke, dust, white cores).")]
        private ParticleSystem[] untinted;

        private GameObject _prefab;
        private ParticleSystem[] _systems;
        private ParticleSystem.MinMaxGradient[] _baseColors;
        private TrailRenderer[] _trails;
        private Gradient[] _baseTrailGradients;
        private MeshRenderer[] _meshes;
        private IFxDriver[] _drivers;
        private MaterialPropertyBlock _block;
        private Color _tint = Color.white;
        private bool _released;
        private bool _stopping;
        private float _stopTime;
        private float _playTime;

        public bool Held { get; private set; }

        public static FxPrefabInstance Spawn(GameObject prefab, Vector3 position, Quaternion rotation, float scale, in FxParams parameters,
            bool held = false)
        {
            if (!Pools.TryGetValue(prefab, out Pool pool))
                Pools[prefab] = pool = new Pool();

            pool.All.RemoveAll(existing => existing == null);

            FxPrefabInstance instance = null;
            while (pool.Idle.Count > 0 && instance == null)
                instance = Usable(pool, pool.Idle.Pop());

            // Cap reached: take over the oldest copy still on screen instead of making a new one.
            if (instance == null && pool.All.Count >= MaxInstancesOf(prefab))
                instance = Usable(pool, OldestActive(pool));

            if (instance == null)
            {
                GameObject go = Instantiate(prefab, FxRunner.Instance.transform);
                if (!go.TryGetComponent(out instance))
                    instance = go.AddComponent<FxPrefabInstance>();
                instance._prefab = prefab;
                instance.Cache();
                pool.All.Add(instance);
            }
            else
            {
                instance.ResetForReuse();
            }

            Transform t = instance.transform;
            t.SetPositionAndRotation(position, Quaternion.Normalize(rotation));
            t.localScale = Vector3.one * scale;
            instance.gameObject.SetActive(true);
            instance.Held = held;
            instance._released = false;
            instance._stopping = false;
            instance._playTime = Time.time;
            instance.SetTint(parameters.Tint);
            instance.Play(parameters);
            return instance;
        }

        public void SetTint(Color tint)
        {
            _tint = tint;

            for (int i = 0; i < _systems.Length; i++)
            {
                if (untinted != null && System.Array.IndexOf(untinted, _systems[i]) >= 0)
                    continue;

                ParticleSystem.MainModule main = _systems[i].main;
                main.startColor = Multiply(_baseColors[i], tint);
            }

            for (int i = 0; i < _trails.Length; i++)
                _trails[i].colorGradient = Multiply(_baseTrailGradients[i], tint);

            foreach (MeshRenderer mesh in _meshes)
            {
                mesh.GetPropertyBlock(_block);
                _block.SetColor(TintId, tint);
                mesh.SetPropertyBlock(_block);
            }

            foreach (IFxDriver driver in _drivers)
                (driver as IFxTintReceiver)?.SetTint(tint);
        }

        public void SetValue(float value)
        {
            foreach (IFxDriver driver in _drivers)
                (driver as IFxValueReceiver)?.SetValue(value);
        }

        public void SetSpeed(float value)
        {
            foreach (IFxDriver driver in _drivers)
                (driver as IFxSpeedReceiver)?.SetSpeed(value);
        }

        public void SetPoints(Vector3 start, Vector3 end)
        {
            foreach (IFxDriver driver in _drivers)
                (driver as IFxPointsReceiver)?.SetPoints(start, end);
        }

        // A copy that lost parts (something destroyed a child) is thrown away instead of reused.
        private static FxPrefabInstance Usable(Pool pool, FxPrefabInstance candidate)
        {
            if (candidate == null || !candidate.IsBroken)
                return candidate;

            pool.All.Remove(candidate);
            Destroy(candidate.gameObject);
            return null;
        }

        private bool IsBroken
        {
            get
            {
                foreach (ParticleSystem system in _systems)
                    if (system == null) return true;
                foreach (TrailRenderer trail in _trails)
                    if (trail == null) return true;
                foreach (MeshRenderer mesh in _meshes)
                    if (mesh == null) return true;
                return false;
            }
        }

        private static int MaxInstancesOf(GameObject prefab) =>
            prefab.TryGetComponent(out FxPrefabInstance settings) ? settings.maxInstances : DefaultMaxInstances;

        // Oldest one-shot still playing. Held effects (orbs, circles, auras) are never taken over.
        private static FxPrefabInstance OldestActive(Pool pool)
        {
            FxPrefabInstance oldest = null;
            foreach (FxPrefabInstance candidate in pool.All)
            {
                if (candidate == null || candidate._released || candidate.Held)
                    continue;

                if (oldest == null || candidate._playTime < oldest._playTime)
                    oldest = candidate;
            }

            return oldest;
        }

        // Cut whatever is still showing so the copy can start over cleanly.
        private void ResetForReuse()
        {
            foreach (ParticleSystem system in _systems)
                system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            foreach (TrailRenderer trail in _trails)
                trail.Clear();
        }

        // Stops emitting; the effect finishes what is already on screen, then returns to the pool.
        public void Kill()
        {
            if (_released || _stopping)
                return;

            _stopping = true;
            _stopTime = Time.time;
            Held = false;

            foreach (ParticleSystem system in _systems)
                system.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            foreach (TrailRenderer trail in _trails)
                trail.emitting = false;
            foreach (IFxDriver driver in _drivers)
                driver.Stop();
        }

        private void Play(in FxParams parameters)
        {
            foreach (TrailRenderer trail in _trails)
            {
                trail.Clear();
                trail.emitting = true;
            }

            foreach (IFxDriver driver in _drivers)
                driver.Play(parameters);

            foreach (ParticleSystem system in _systems)
            {
                system.Clear(false);
                system.Play(false);
            }
        }

        private void Update()
        {
            if (IsBroken)
            {
                if (_prefab != null && Pools.TryGetValue(_prefab, out Pool pool))
                    pool.All.Remove(this);
                Destroy(gameObject);
                return;
            }

            if (Held || Time.time - _playTime < 0.05f)
                return;

            if (!IsFinished())
                return;

            Release();
        }

        private bool IsFinished()
        {
            foreach (ParticleSystem system in _systems)
            {
                if (system.IsAlive(false))
                    return false;
            }

            foreach (IFxDriver driver in _drivers)
            {
                if (driver.IsPlaying)
                    return false;
            }

            // Trails stay visible for their time after they stop emitting.
            foreach (TrailRenderer trail in _trails)
            {
                if (trail.emitting || _stopping && Time.time - _stopTime < trail.time)
                    return false;
            }

            return true;
        }

        private void Release()
        {
            if (_released)
                return;

            _released = true;
            foreach (TrailRenderer trail in _trails)
                trail.Clear();
            gameObject.SetActive(false);

            if (_prefab != null && Pools.TryGetValue(_prefab, out Pool pool))
                pool.Idle.Push(this);
        }

        private void Cache()
        {
            _systems = GetComponentsInChildren<ParticleSystem>(true);
            _baseColors = new ParticleSystem.MinMaxGradient[_systems.Length];
            for (int i = 0; i < _systems.Length; i++)
            {
                ParticleSystem.MainModule main = _systems[i].main;
                _baseColors[i] = main.startColor;

                // Pooled copies are reused, so they must never destroy or disable themselves when they finish
                // (imported effects such as Batslash ship with Stop Action = Destroy).
                main.stopAction = ParticleSystemStopAction.None;
            }

            _trails = GetComponentsInChildren<TrailRenderer>(true);
            _baseTrailGradients = new Gradient[_trails.Length];
            for (int i = 0; i < _trails.Length; i++)
                _baseTrailGradients[i] = _trails[i].colorGradient;

            _meshes = GetComponentsInChildren<MeshRenderer>(true);
            _drivers = GetComponentsInChildren<IFxDriver>(true);
            _block = new MaterialPropertyBlock();
        }

        private static ParticleSystem.MinMaxGradient Multiply(ParticleSystem.MinMaxGradient gradient, Color tint)
        {
            switch (gradient.mode)
            {
                case ParticleSystemGradientMode.Color:
                    return new ParticleSystem.MinMaxGradient(gradient.color * tint);
                case ParticleSystemGradientMode.TwoColors:
                    return new ParticleSystem.MinMaxGradient(gradient.colorMin * tint, gradient.colorMax * tint);
                case ParticleSystemGradientMode.Gradient:
                    return new ParticleSystem.MinMaxGradient(Multiply(gradient.gradient, tint));
                case ParticleSystemGradientMode.TwoGradients:
                    return new ParticleSystem.MinMaxGradient(Multiply(gradient.gradientMin, tint), Multiply(gradient.gradientMax, tint));
                default:
                    return gradient;
            }
        }

        internal static Gradient Multiply(Gradient gradient, Color tint)
        {
            var result = new Gradient { mode = gradient.mode };
            GradientColorKey[] colors = gradient.colorKeys;
            for (int i = 0; i < colors.Length; i++)
                colors[i].color *= tint;
            result.SetKeys(colors, gradient.alphaKeys);
            return result;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Pools.Clear();
    }
}
