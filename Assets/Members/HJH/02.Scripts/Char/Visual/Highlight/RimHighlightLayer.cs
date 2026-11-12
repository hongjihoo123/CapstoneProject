using System.Collections.Generic;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Visual.Highlight
{
    // (b) 적 몸에 약한 림라이트. 적 렌더러에 가산 머티리얼(HJH/EnemyRim)을 하나 덧붙임 → 적 셰이더가 뭐든 동작
    public sealed class RimHighlightLayer : HighlightLayer
    {
        private const string ShaderPath = "HJH/EnemyRim";   // Resources/HJH/EnemyRim.shader
        private static readonly int ColorId = Shader.PropertyToID("_RimColor");
        private static readonly int PowerId = Shader.PropertyToID("_RimPower");
        private static readonly int FillId = Shader.PropertyToID("_Fill");

        [SerializeField, Tooltip("A = 세기")] private Color rimColor = new(1f, 0.38f, 0.28f, 0.85f);
        [SerializeField, Range(0.5f, 8f)] private float rimPower = 3f;
        [SerializeField, Range(0f, 0.5f), Tooltip("몸 전체를 살짝 밝히는 양 (배경보다 명도↑)")] private float fill = 0.06f;

        private Material _material;
        private readonly Dictionary<HighlightTarget, Renderer[]> _renderers = new();

        private Material Material
        {
            get
            {
                if (_material != null)
                    return _material;
                Shader shader = Resources.Load<Shader>(ShaderPath);
                if (shader == null)
                    return null;
                _material = new Material(shader) { name = "EnemyRim (runtime)", hideFlags = HideFlags.HideAndDontSave };
                return _material;
            }
        }

        private void Update()
        {
            if (_material == null)
                return;
            _material.SetColor(ColorId, rimColor);
            _material.SetFloat(PowerId, rimPower);
            _material.SetFloat(FillId, fill);
        }

        protected override void Attach(HighlightTarget target)
        {
            Material rim = Material;
            if (rim == null)
                return;
            Update();

            var list = new List<Renderer>();
            foreach (Renderer r in target.Root.GetComponentsInChildren<Renderer>(true))
            {
                if (r is not (MeshRenderer or SkinnedMeshRenderer) || r.sharedMaterials.Length == 0)
                    continue;
                if (r.gameObject.layer == LayerMask.NameToLayer("MiniMap"))
                    continue;
                var materials = new List<Material>(r.sharedMaterials);
                if (!materials.Contains(rim))
                {
                    materials.Add(rim);
                    r.sharedMaterials = materials.ToArray();
                }
                list.Add(r);
            }
            _renderers[target] = list.ToArray();
        }

        protected override void Detach(HighlightTarget target)
        {
            if (!_renderers.TryGetValue(target, out Renderer[] renderers))
                return;
            _renderers.Remove(target);
            foreach (Renderer r in renderers)
            {
                if (r == null)
                    continue;
                var materials = new List<Material>(r.sharedMaterials);
                if (materials.RemoveAll(m => m == _material) > 0)
                    r.sharedMaterials = materials.ToArray();
            }
        }
    }
}
