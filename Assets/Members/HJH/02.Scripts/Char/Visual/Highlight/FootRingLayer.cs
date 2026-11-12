using System.Collections.Generic;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Visual.Highlight
{
    // (a) 발밑 링 (SoD 식): 적 = 빨강, 플레이어 = 초록. 바닥에 납작한 링 한 장을 깔고 매 프레임 발밑 땅 높이에 맞춤.
    // 링은 캐릭터 자식이 아니라 이 오브젝트 밑에 둠 (캐릭터 회전·크기를 안 따라가게, 적 프리팹을 안 건드리게)
    public sealed class FootRingLayer : HighlightLayer
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField, Tooltip("링 머티리얼 (투명, _BaseColor 로 색)")] private Material ringMaterial;
        [SerializeField] private Color enemyColor = new(1f, 0.16f, 0.12f, 0.9f);
        [SerializeField] private Color playerColor = new(0.35f, 1f, 0.45f, 0.85f);
        [SerializeField, Tooltip("링 지름 = 몸 반지름 x 2 x 이 값")] private float sizeScale = 1.55f;
        [SerializeField, Tooltip("최소 지름 (m)")] private float minDiameter = 1.1f;
        [SerializeField, Tooltip("바닥에서 띄우는 높이 (m). 바닥에 묻히지 않게")] private float lift = 0.04f;
        [SerializeField, Tooltip("발밑 땅을 찾을 레이어")] private LayerMask groundLayers;

        [Header("숨쉬기 (적만)")]
        [SerializeField, Range(0f, 1f)] private float pulseAlpha = 0.25f;
        [SerializeField] private float pulseSpeed = 2.4f;

        private readonly Dictionary<HighlightTarget, MeshRenderer> _rings = new();
        private readonly Stack<MeshRenderer> _pool = new();
        private MaterialPropertyBlock _block;
        private static Mesh _quad;

        private void Awake()
        {
            if (groundLayers.value == 0)
                groundLayers = LayerMask.GetMask("Ground", "Obstacle", "Default");
            _block = new MaterialPropertyBlock();
        }

        protected override void Attach(HighlightTarget target)
        {
            MeshRenderer ring = _pool.Count > 0 ? _pool.Pop() : CreateRing();
            ring.gameObject.SetActive(true);
            float diameter = Mathf.Max(minDiameter, target.Radius * 2f * sizeScale);
            ring.transform.localScale = new Vector3(diameter, diameter, 1f);
            _rings[target] = ring;
            Place(target, ring);
        }

        protected override void Detach(HighlightTarget target)
        {
            if (!_rings.TryGetValue(target, out MeshRenderer ring))
                return;
            _rings.Remove(target);
            if (ring == null)
                return;
            ring.gameObject.SetActive(false);
            _pool.Push(ring);
        }

        protected override void Tick(IEnumerable<HighlightTarget> attached)
        {
            float pulse = 1f - pulseAlpha * (0.5f + 0.5f * Mathf.Sin(Time.time * pulseSpeed * Mathf.PI * 2f * 0.5f));
            foreach (HighlightTarget target in attached)
            {
                if (!_rings.TryGetValue(target, out MeshRenderer ring) || ring == null)
                    continue;
                Place(target, ring);

                Color color = target.IsEnemy ? enemyColor : playerColor;
                if (target.IsEnemy)
                    color.a *= pulse;
                _block.SetColor(BaseColorId, color);
                ring.SetPropertyBlock(_block);
            }
        }

        private void Place(HighlightTarget target, MeshRenderer ring)
        {
            Vector3 foot = target.Root.position;
            if (Physics.Raycast(foot + Vector3.up * 1.5f, Vector3.down, out RaycastHit hit, 4f, groundLayers, QueryTriggerInteraction.Ignore))
                foot.y = hit.point.y;
            ring.transform.SetPositionAndRotation(foot + Vector3.up * lift, Quaternion.Euler(90f, 0f, 0f));
        }

        private MeshRenderer CreateRing()
        {
            var go = new GameObject("FootRing");
            go.transform.SetParent(transform, false);
            go.layer = gameObject.layer;
            go.AddComponent<MeshFilter>().sharedMesh = Quad;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = ringMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            return renderer;
        }

        // 1x1 정사각형 (앞면 -Z). Euler(90,0,0) 으로 눕히면 위에서 보임
        private static Mesh Quad
        {
            get
            {
                if (_quad != null)
                    return _quad;
                _quad = new Mesh { name = "FootRingQuad" };
                _quad.SetVertices(new[] { new Vector3(-0.5f, -0.5f), new Vector3(0.5f, -0.5f), new Vector3(-0.5f, 0.5f), new Vector3(0.5f, 0.5f) });
                _quad.SetUVs(0, new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) });
                _quad.SetNormals(new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back });
                _quad.SetTriangles(new[] { 0, 2, 1, 2, 3, 1 }, 0);
                _quad.RecalculateBounds();
                return _quad;
            }
        }
    }
}
