using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Assets.Members.HJH._02.Scripts.Char.Visual.Highlight
{
    // 캐릭터(적·플레이어)만 비추는 조명. 바닥·바위는 그대로 어둡고 캐릭터 몸만 배경보다 밝게 → SoD 식 "본체가 배경보다 밝음".
    //  - URP 렌더링 레이어 사용: 캐릭터 렌더러에 레이어 비트(기본 1 = "Light Layer 1")를 더하고, 이 조명은 그 비트만 비춤.
    //    맵의 다른 조명(태양·발광 라이트)은 Default 비트만 비추므로 영향 없음.
    //  - 방향광 1개라 적이 많아도 비용이 같음 (점광원을 적마다 다는 방식은 바닥에서 라이트 수 제한에 걸려 효과가 없었음)
    //  - 적 프리팹·머티리얼(JJH)은 안 건드림. 실행 중에 렌더러의 renderingLayerMask 만 바꿨다가 떼어낼 때 되돌림
    public sealed class CharacterLightLayer : HighlightLayer
    {
        [SerializeField, Range(1, 31), Tooltip("캐릭터용 렌더링 레이어 번호 (Tags and Layers > Rendering Layers). 1 = Light Layer 1")]
        private int renderingLayer = 1;

        [Header("조명")]
        [SerializeField] private Color color = new(1f, 0.95f, 0.88f);
        [SerializeField, Tooltip("세기. 높이면 캐릭터가 더 떠 보임")] private float intensity = 0.9f;
        [SerializeField, Tooltip("빛이 내려오는 높이 각도")] private float pitch = 50f;
        [SerializeField, Tooltip("메인 카메라 기준 좌우 각도 (0 = 카메라 쪽에서 비춤 → 화면에 보이는 면이 밝음)")] private float yawFromCamera = 20f;

        private readonly Dictionary<HighlightTarget, Renderer[]> _renderers = new();
        private Light _light;

        private uint Bit => 1u << renderingLayer;

        protected override void Attach(HighlightTarget target)
        {
            EnsureLight();
            var list = new List<Renderer>();
            foreach (Renderer r in target.Root.GetComponentsInChildren<Renderer>(true))
            {
                if (r is not (MeshRenderer or SkinnedMeshRenderer))
                    continue;
                if (r.gameObject.layer == LayerMask.NameToLayer("MiniMap"))
                    continue;
                r.renderingLayerMask |= Bit;
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
                if (r != null)
                    r.renderingLayerMask &= ~Bit;
        }

        private float _nextRescan;

        protected override void Tick(IEnumerable<HighlightTarget> attached)
        {
            if (_light == null)
                return;

            // 캐릭터 모델은 나중에 생기기도 함 (플레이어 모델은 실행 중에 스폰, 캐릭터 교체) → 가끔 다시 훑어서 새 렌더러에도 비트
            if (Time.unscaledTime >= _nextRescan)
            {
                _nextRescan = Time.unscaledTime + 0.5f;
                var targets = new List<HighlightTarget>(attached);
                foreach (HighlightTarget t in targets)
                    if (t.Root != null)
                        Attach(t);
            }
            _light.color = color;
            _light.intensity = intensity;
            Camera cam = Camera.main;
            float yaw = (cam != null ? cam.transform.eulerAngles.y : 45f) + yawFromCamera;
            _light.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (_light != null)
                _light.enabled = false;
        }

        private void EnsureLight()
        {
            if (_light != null)
            {
                _light.enabled = true;
                return;
            }

            var go = new GameObject("CharacterKeyLight");
            go.transform.SetParent(transform, false);
            _light = go.AddComponent<Light>();
            _light.type = LightType.Directional;
            _light.shadows = LightShadows.None;
            _light.renderMode = LightRenderMode.ForcePixel;
            _light.lightmapBakeType = LightmapBakeType.Realtime;

            var data = go.GetComponent<UniversalAdditionalLightData>();
            if (data == null)
                data = go.AddComponent<UniversalAdditionalLightData>();
            data.renderingLayers = (RenderingLayerMask)Bit;
            data.shadowRenderingLayers = (RenderingLayerMask)Bit;
        }
    }
}
