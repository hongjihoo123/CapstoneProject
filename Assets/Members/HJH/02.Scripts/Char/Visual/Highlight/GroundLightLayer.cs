using System.Collections.Generic;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Visual.Highlight
{
    // (c) 적 머리 위에 약한 점광원 → 적 주변 바닥과 적 몸이 살짝 밝아짐. 그림자 없음.
    // 적이 많으면 라이트 수가 늘어남 (URP 오브젝트당 추가 라이트 제한) → 비용이 가장 큰 방식
    public sealed class GroundLightLayer : HighlightLayer
    {
        [SerializeField] private Color color = new(1f, 0.55f, 0.45f);
        [SerializeField] private float intensity = 1.6f;
        [SerializeField, Tooltip("빛이 닿는 반경 (m)")] private float range = 3.5f;
        [SerializeField, Tooltip("발 기준 높이 (m)")] private float height = 1.8f;

        private readonly Dictionary<HighlightTarget, Light> _lights = new();
        private readonly Stack<Light> _pool = new();

        protected override void Attach(HighlightTarget target)
        {
            Light light = _pool.Count > 0 ? _pool.Pop() : CreateLight();
            light.gameObject.SetActive(true);
            _lights[target] = light;
        }

        protected override void Detach(HighlightTarget target)
        {
            if (!_lights.TryGetValue(target, out Light light))
                return;
            _lights.Remove(target);
            if (light == null)
                return;
            light.gameObject.SetActive(false);
            _pool.Push(light);
        }

        protected override void Tick(IEnumerable<HighlightTarget> attached)
        {
            foreach (HighlightTarget target in attached)
            {
                if (!_lights.TryGetValue(target, out Light light) || light == null)
                    continue;
                light.transform.position = target.Root.position + Vector3.up * height;
                light.color = color;
                light.intensity = intensity;
                light.range = range;
            }
        }

        private Light CreateLight()
        {
            var go = new GameObject("EnemyGroundLight");
            go.transform.SetParent(transform, false);
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.shadows = LightShadows.None;
            light.renderMode = LightRenderMode.ForcePixel;
            return light;
        }
    }
}
