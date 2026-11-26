using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Assets.Members.HJH._02.Scripts.Char.Visual
{
    public static class GunnerFx
    {
        public static readonly Color Amber = new(1f, 0.62f, 0.18f);
        public static readonly Color Teal = new(0.25f, 1f, 0.85f);

        public static Color Hot(Color c, float whiten = 0.35f) => Color.Lerp(c, Color.white, whiten);

        public static void Shot(Vector3 muzzle, Vector3 direction, Color color, bool empowered)
        {
            Fx.Muzzle(muzzle, direction, Hot(color, 0.6f), false);
            Flash(muzzle, color, empowered ? 6f : 2.2f, empowered ? 4f : 2.5f, empowered ? 0.1f : 0.05f);
            if (empowered)
            {
                Fx.Shockwave(muzzle, Hot(color), 1.2f);
                ScreenPulse(0.35f, 0.18f);
            }
        }

        public static void DashStart(Transform who, Vector3 direction, Color color, float duration)
        {
            Vector3 p = who.position;
            Fx.Shockwave(p + Vector3.up * 0.05f, Hot(color), 1.3f);
            Fx.Hit(p + Vector3.up * 0.4f, -direction, Hot(color, 0.5f), false);
            Flash(p + Vector3.up, color, 3f, 4f, 0.12f);
            Afterimages(who, color, Mathf.Clamp(Mathf.RoundToInt(duration / 0.045f), 3, 6), 0.045f);
        }

        public static void DashBlast(Vector3 apex, Vector3 direction, float radius, float angle, Color color)
        {
            Fx.ConeBlast(apex, direction, radius, angle, Hot(color, 0.2f));
            Vector3 front = apex + Vector3.up + direction * 0.7f;
            Fx.CannonMuzzle(front, direction, Hot(color, 0.7f));
            Fx.Shockwave(apex + direction * 1.5f, Hot(color), radius * 0.55f);
            Fx.After(0.06f, () => Fx.Shockwave(apex + direction * 2.5f, color, radius * 0.8f));
            Flash(front + direction, color, 9f, radius * 1.3f, 0.16f);
            ScreenPulse(0.7f, 0.25f);
        }

        public static void BlastHit(Vector3 point, Vector3 direction, Color color) =>
            Fx.Hit(point, direction, Hot(color, 0.5f), true);

        public static void CannonCharging(Vector3 tip, Color color, float charge) =>
            Flash(tip, color, Mathf.Lerp(1f, 5f, charge * charge), 3f, 0.05f);

        public static void CannonFire(Vector3 tip, Vector3 feet, Vector3 direction, Color color)
        {
            Fx.CannonMuzzle(tip, direction, Hot(color, 0.15f));
            Fx.CannonMuzzle(tip + direction * 0.4f, direction, Hot(color, 0.8f));
            Fx.Muzzle(tip + direction * 0.8f, direction, Hot(color, 0.5f), true);
            Fx.Shockwave(feet, Hot(color), 2.6f);
            Fx.After(0.05f, () => Fx.Shockwave(feet, color, 4f));
            Flash(tip, color, 14f, 7f, 0.2f);
            ScreenPulse(1f, 0.32f);
            global::Assets.Members.HJH._02.Scripts.Char.Visual.ScreenPulse.ImpactFrame(0.05f);
        }

        public static void CannonExplode(Vector3 ground, Color color)
        {
            Fx.Explosion(ground, 2.2f, Hot(color, 0.7f), 2f);
            Fx.After(0.07f, () => Fx.Explosion(ground, 3.8f, color, 2f));
            Fx.Shockwave(ground + Vector3.up * 0.05f, Hot(color), 3.5f);
            Fx.After(0.12f, () => Fx.Shockwave(ground + Vector3.up * 0.05f, color, 5.5f));
            Flash(ground + Vector3.up * 1.2f, color, 16f, 9f, 0.25f);
            ScreenPulse(0.9f, 0.3f);
        }

        public static void ChargedShot(Vector3 start, Vector3 end, Color color, float width)
        {
            Vector3 dir = end - start;
            Fx.Beam(start, end, Hot(color, 0.75f), width * 0.45f, 0.18f);
            Fx.CannonMuzzle(start, dir, Hot(color, 0.5f));
            Flash(start, color, 10f, 6f, 0.16f);
            ScreenPulse(0.75f, 0.24f);
        }

        private const int LightPool = 8;
        private static readonly Light[] _lights = new Light[LightPool];
        private static int _nextLight;

        public static void Flash(Vector3 position, Color color, float intensity, float range, float duration)
        {
            Light light = NextLight();
            if (light == null) return;
            light.transform.position = position;
            light.color = Color.Lerp(color, Color.white, 0.25f);
            light.range = range;
            light.intensity = intensity;
            light.enabled = true;
            FxRunner.Instance.Add(new LightFade(light, intensity, duration));
        }

        private static Light NextLight()
        {
            for (int i = 0; i < LightPool; i++)
            {
                int k = (_nextLight + i) % LightPool;
                if (_lights[k] == null)
                {
                    var go = new GameObject("GunnerFx_Flash") { hideFlags = HideFlags.DontSave };
                    Object.DontDestroyOnLoad(go);
                    _lights[k] = go.AddComponent<Light>();
                    _lights[k].type = LightType.Point;
                    _lights[k].shadows = LightShadows.None;
                    _lights[k].enabled = false;
                }
                if (!_lights[k].enabled) { _nextLight = k + 1; return _lights[k]; }
            }
            _nextLight = (_nextLight + 1) % LightPool;
            return _lights[_nextLight];
        }

        private sealed class LightFade : FxInstance
        {
            private readonly Light _light;
            private readonly float _peak, _duration;
            private float _t;

            public LightFade(Light light, float peak, float duration) { _light = light; _peak = peak; _duration = Mathf.Max(0.01f, duration); }

            public override bool Tick(float deltaTime)
            {
                if (_light == null) return false;
                _t += deltaTime;
                float k = 1f - _t / _duration;
                if (k <= 0f) { _light.enabled = false; return false; }
                _light.intensity = _peak * k * k;
                return true;
            }
        }

        private static Volume _volume;
        private static float _pulse, _pulseDecay;

        public static void ScreenPulse(float strength, float duration)
        {
            if (!EnsureVolume()) return;
            if (strength >= _pulse) { _pulse = strength; _pulseDecay = strength / Mathf.Max(0.02f, duration); }
            if (!_pulseRunning) { _pulseRunning = true; FxRunner.Instance.Add(new PulseRunner()); }
        }

        private static bool _pulseRunning;

        private static bool EnsureVolume()
        {
            if (_volume != null) return true;
            var go = new GameObject("GunnerFx_ScreenPulse") { hideFlags = HideFlags.DontSave };
            Object.DontDestroyOnLoad(go);
            _volume = go.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 100f;
            _volume.weight = 0f;
            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.hideFlags = HideFlags.DontSave;
            Bloom bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(2.2f);
            bloom.threshold.Override(0.75f);
            bloom.scatter.Override(0.75f);
            ChromaticAberration ca = profile.Add<ChromaticAberration>(true);
            ca.intensity.Override(0.3f);
            LensDistortion lens = profile.Add<LensDistortion>(true);
            lens.intensity.Override(-0.18f);
            lens.scale.Override(1f);
            _volume.sharedProfile = profile;
            return true;
        }

        private sealed class PulseRunner : FxInstance
        {
            public override bool Tick(float deltaTime)
            {
                if (_volume == null) { _pulseRunning = false; return false; }
                _pulse = Mathf.Max(0f, _pulse - _pulseDecay * deltaTime);
                _volume.weight = _pulse;
                if (_pulse > 0f) return true;
                _pulseRunning = false;
                return false;
            }
        }

        private static Material _ghostMaterial;
        private static readonly int ColorId = Shader.PropertyToID("_BaseColor");

        public static void Afterimages(Transform who, Color color, int count, float interval)
        {
            for (int i = 0; i < count; i++)
                Fx.After(i * interval, () => Ghost(who, color));
        }

        private static void Ghost(Transform who, Color color)
        {
            if (who == null) return;
            Material mat = GhostMaterial();
            if (mat == null) return;
            foreach (SkinnedMeshRenderer smr in who.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (!smr.enabled || !smr.gameObject.activeInHierarchy) continue;
                var mesh = new Mesh();
                smr.BakeMesh(mesh, true);
                var go = new GameObject("GunnerFx_Ghost") { hideFlags = HideFlags.DontSave };
                go.transform.SetPositionAndRotation(smr.transform.position, smr.transform.rotation);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                FxRunner.Instance.Add(new GhostFade(go, mesh, mr, Hot(color, 0.3f), 0.28f));
            }
        }

        private static Material GhostMaterial()
        {
            if (_ghostMaterial != null) return _ghostMaterial;
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) return null;
            _ghostMaterial = new Material(shader) { hideFlags = HideFlags.DontSave, name = "GunnerFx_Ghost" };
            _ghostMaterial.SetFloat("_Surface", 1f);
            _ghostMaterial.SetFloat("_Blend", 2f);
            _ghostMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            _ghostMaterial.SetFloat("_DstBlend", (float)BlendMode.One);
            _ghostMaterial.SetFloat("_ZWrite", 0f);
            _ghostMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            _ghostMaterial.renderQueue = (int)RenderQueue.Transparent;
            return _ghostMaterial;
        }

        private sealed class GhostFade : FxInstance
        {
            private readonly GameObject _go;
            private readonly Mesh _mesh;
            private readonly MeshRenderer _renderer;
            private readonly Color _color;
            private readonly float _life;
            private readonly MaterialPropertyBlock _block = new();
            private float _t;

            public GhostFade(GameObject go, Mesh mesh, MeshRenderer renderer, Color color, float life)
            { _go = go; _mesh = mesh; _renderer = renderer; _color = color; _life = life; }

            public override bool Tick(float deltaTime)
            {
                _t += deltaTime;
                float k = 1f - _t / _life;
                if (k <= 0f || _go == null)
                {
                    if (_go != null) Object.Destroy(_go);
                    if (_mesh != null) Object.Destroy(_mesh);
                    return false;
                }
                Color c = _color * (1.6f * k);
                c.a = 0.55f * k;
                _block.SetColor(ColorId, c);
                _renderer.SetPropertyBlock(_block);
                return true;
            }
        }
    }
}
