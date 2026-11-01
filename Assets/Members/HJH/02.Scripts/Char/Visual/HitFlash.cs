using System.Collections.Generic;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Visual
{
    // 맞은 적이 순간 하얗게(또는 원소 색으로) 번쩍. 적의 셰이더가 무엇이든 동작하도록
    // 렌더러에 가산 머티리얼(HJH/HitFlash)을 잠깐 덧붙였다가 빠르게 지운다.
    // 대상에 처음 맞을 때 이 컴포넌트가 자동으로 붙는다. HitFlash.Flash(대상, 색) 만 부르면 됨.
    public class HitFlash : MonoBehaviour
    {
        private const string ShaderPath = "HJH/HitFlash";   // Resources/HJH/HitFlash.shader
        private static readonly int ColorId = Shader.PropertyToID("_FlashColor");
        private static Material _material;
        private static bool _loadFailed;

        private Renderer[] _renderers;
        private Material[][] _originals;
        private MaterialPropertyBlock _block;
        private Color _color;
        private float _start;
        private float _duration;
        private bool _on;

        public static void Flash(Component target, Color color, float duration = 0.12f)
        {
            if (target == null || Material == null)
                return;

            if (!target.TryGetComponent(out HitFlash flash))
                flash = target.gameObject.AddComponent<HitFlash>();
            flash.Begin(color, duration);
        }

        private static Material Material
        {
            get
            {
                if (_material != null || _loadFailed)
                    return _material;
                var shader = Resources.Load<Shader>(ShaderPath);
                if (shader == null)
                {
                    _loadFailed = true;
                    return null;
                }
                _material = new Material(shader) { name = "HitFlash (runtime)", hideFlags = HideFlags.HideAndDontSave };
                return _material;
            }
        }

        private void Begin(Color color, float duration)
        {
            if (_renderers == null)
                Collect();

            if (!_on)
            {
                for (int i = 0; i < _renderers.Length; i++)
                {
                    if (_renderers[i] == null)
                        continue;
                    var list = new List<Material>(_originals[i]) { Material };
                    _renderers[i].sharedMaterials = list.ToArray();
                }
                _on = true;
            }

            _color = color;
            _start = Time.unscaledTime;
            _duration = Mathf.Max(0.03f, duration);
            Apply(1f);
        }

        private void Collect()
        {
            var found = new List<Renderer>();
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
            {
                if (r is MeshRenderer or SkinnedMeshRenderer && r.sharedMaterials.Length > 0)
                    found.Add(r);
            }

            _renderers = found.ToArray();
            _originals = new Material[_renderers.Length][];
            for (int i = 0; i < _renderers.Length; i++)
                _originals[i] = _renderers[i].sharedMaterials;
            _block = new MaterialPropertyBlock();
        }

        private void Update()
        {
            if (!_on)
                return;

            float t = (Time.unscaledTime - _start) / _duration;
            if (t >= 1f)
            {
                Restore();
                return;
            }

            float k = 1f - t;
            Apply(k * k);
        }

        private void Apply(float strength)
        {
            Color c = _color;
            c.a = strength;
            foreach (Renderer r in _renderers)
            {
                if (r == null)
                    continue;
                r.GetPropertyBlock(_block);
                _block.SetColor(ColorId, c);
                r.SetPropertyBlock(_block);
            }
        }

        private void Restore()
        {
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null)
                    _renderers[i].sharedMaterials = _originals[i];
            }
            Apply(0f);
            _on = false;
        }

        private void OnDisable()
        {
            if (_on)
                Restore();
        }
    }
}
