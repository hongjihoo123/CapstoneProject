using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using Members.JJH._02_Scripts.ElementsSystem;
using Members.KYR._01_Scripts;
using UnityEngine;
using UnityEngine.Rendering;

namespace Assets.Members.HJH.Environment
{
    // 바닥 안개 연출 담당 (씬에 안개 판이 있으면 저절로 생김, 씬 파일 수정 없음)
    //  1) 더 짙고 높게: 안개 판을 raiseHeight 만큼 올리고, 짙기·두께를 배율로 키움 (플레이 중에만, 머티리얼 에셋은 그대로)
    //  2) 플레이어 주변은 옅게: 게임 카메라에서만 플레이어 둘레가 걷힘 (씬뷰는 그대로)
    //  3) 스킬 반응: 스킬이 터진 자리(SkillImpacts)에 구멍이 확 뚫리고, 테두리로 안개가 밀려났다가 몇 초에 걸쳐 다시 참
    //     바람 스킬은 더 넓게 걷어냄. 스킬 코드는 안개를 모름 → 새 스킬도 자동으로 반응
    public class JungleFogController : MonoBehaviour
    {
        private const int MaxHoles = 8;   // 셰이더 JW_FOG_HOLES 와 같아야 함

        [Header("짙기·높이")]
        [SerializeField, Tooltip("안개 판을 이만큼 올림 (m)")] private float raiseHeight = 0.8f;
        [SerializeField] private float densityMultiplier = 1.7f;
        [SerializeField] private float thicknessMultiplier = 1.4f;

        [Header("플레이어 주변")]
        [SerializeField] private float revealRadius = 2.5f;
        [SerializeField] private float revealSoftness = 3.5f;
        [SerializeField, Range(0f, 1f), Tooltip("플레이어 바로 옆 안개 짙기 (0 = 완전히 걷힘)")] private float revealMin = 0.2f;

        [Header("스킬 반응")]
        [SerializeField, Tooltip("터진 범위보다 이만큼 넓게 걷힘")] private float holeScale = 1.5f;
        [SerializeField] private float windHoleScale = 2.2f;
        [SerializeField, Tooltip("걷힌 안개가 다시 다 차기까지 (초)")] private float refillSeconds = 3.5f;
        [SerializeField] private float minRadius = 1f;

        private static readonly int HolesId = Shader.PropertyToID("_JungleFogHoles");
        private static readonly int HoleCountId = Shader.PropertyToID("_JungleFogHoleCount");
        private static readonly int PlayerPosId = Shader.PropertyToID("_JunglePlayerPos");
        private static readonly int DensityId = Shader.PropertyToID("_Density");
        private static readonly int ThicknessId = Shader.PropertyToID("_FogThickness");
        private static readonly int RevealRadiusId = Shader.PropertyToID("_RevealRadius");
        private static readonly int RevealSoftnessId = Shader.PropertyToID("_RevealSoftness");
        private static readonly int RevealMinId = Shader.PropertyToID("_RevealMin");

        private struct Hole
        {
            public Vector2 Center;
            public float Radius;
            public float Age;
        }

        private readonly List<Hole> _holes = new();
        private readonly Vector4[] _holeData = new Vector4[MaxHoles];
        private readonly List<Renderer> _fog = new();
        private Transform _player;
        private float _nextPlayerSearch;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindAnyObjectByType<JungleFogController>() != null || FindFogRenderers().Count == 0)
                return;
            new GameObject("[JungleFog]").AddComponent<JungleFogController>();
        }

        // 이름 말고 셰이더로 찾음 (씬마다 오브젝트 이름이 바뀜)
        private static List<Renderer> FindFogRenderers()
        {
            var result = new List<Renderer>();
            foreach (Renderer r in FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                Material m = r.sharedMaterial;
                if (m != null && m.shader != null && m.shader.name.Contains("GroundFog"))
                    result.Add(r);
            }
            return result;
        }

        private void OnEnable()
        {
            SkillImpacts.ImpactedBy += OnSkillImpact;
            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
            Shader.SetGlobalVectorArray(HolesId, _holeData);   // 배열 크기는 처음 넣을 때 정해짐 → 항상 8칸
            Shader.SetGlobalFloat(HoleCountId, 0f);
        }

        private void OnDisable()
        {
            SkillImpacts.ImpactedBy -= OnSkillImpact;
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            Shader.SetGlobalFloat(HoleCountId, 0f);
            Shader.SetGlobalVector(PlayerPosId, Vector4.zero);
        }

        private void Start()
        {
            _fog.AddRange(FindFogRenderers());
            var block = new MaterialPropertyBlock();
            foreach (Renderer r in _fog)
            {
                r.transform.position += Vector3.up * raiseHeight;
                Material m = r.sharedMaterial;
                r.GetPropertyBlock(block);
                if (m.HasProperty(DensityId))
                    block.SetFloat(DensityId, m.GetFloat(DensityId) * densityMultiplier);
                if (m.HasProperty(ThicknessId))
                    block.SetFloat(ThicknessId, m.GetFloat(ThicknessId) * thicknessMultiplier + raiseHeight);
                block.SetFloat(RevealRadiusId, revealRadius);
                block.SetFloat(RevealSoftnessId, revealSoftness);
                block.SetFloat(RevealMinId, revealMin);
                r.SetPropertyBlock(block);
            }
        }

        private void OnSkillImpact(Vector3 position, float radius, SkillData skill)
        {
            bool wind = skill != null && skill.Element == ElementType.Wind;
            float r = Mathf.Max(minRadius, radius) * (wind ? windHoleScale : holeScale);
            var center = new Vector2(position.x, position.z);

            // 장판·돌진처럼 같은 자리에서 계속 터지면 새로 만들지 않고 기존 구멍을 새로 고침
            for (int i = 0; i < _holes.Count; i++)
            {
                Hole h = _holes[i];
                if ((h.Center - center).sqrMagnitude < h.Radius * h.Radius * 0.25f)
                {
                    h.Radius = Mathf.Max(h.Radius, r);
                    h.Age = Mathf.Min(h.Age, 0.2f);
                    _holes[i] = h;
                    return;
                }
            }

            if (_holes.Count >= MaxHoles)
                _holes.RemoveAt(0);   // 제일 오래된 것부터
            _holes.Add(new Hole { Center = center, Radius = r, Age = 0f });
        }

        private void Update()
        {
            if (_player == null && Time.unscaledTime >= _nextPlayerSearch)
            {
                _nextPlayerSearch = Time.unscaledTime + 1f;
                PlayerAgent agent = FindAnyObjectByType<PlayerAgent>();
                if (agent != null)
                    _player = agent.transform;
            }

            int count = 0;
            for (int i = _holes.Count - 1; i >= 0; i--)
            {
                Hole h = _holes[i];
                h.Age += Time.deltaTime;
                if (h.Age >= refillSeconds)
                {
                    _holes.RemoveAt(i);
                    continue;
                }
                _holes[i] = h;
            }

            foreach (Hole h in _holes)
            {
                // 0.15초 만에 확 걷히며 퍼지고 → 잠깐 유지 → 천천히 다시 참
                float t = h.Age;
                float open = Mathf.Clamp01(t / 0.15f);
                float strength = open * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(refillSeconds * 0.3f, refillSeconds, t)));
                float radius = h.Radius * Mathf.Lerp(0.5f, 1f, Mathf.Sqrt(open)) * Mathf.Lerp(1f, 0.8f, t / refillSeconds);
                _holeData[count++] = new Vector4(h.Center.x, h.Center.y, radius, strength);
            }
            for (int i = count; i < MaxHoles; i++)
                _holeData[i] = Vector4.zero;

            Shader.SetGlobalVectorArray(HolesId, _holeData);
            Shader.SetGlobalFloat(HoleCountId, count);
        }

        // 플레이어 주변 걷힘은 게임 카메라에서만 (씬뷰는 안개 그대로 보이게)
        private void OnBeginCamera(ScriptableRenderContext context, Camera cam)
        {
            bool reveal = _player != null && cam.cameraType == CameraType.Game;
            Shader.SetGlobalVector(PlayerPosId, reveal ? (Vector4)_player.position + new Vector4(0f, 0f, 0f, 1f) : Vector4.zero);
        }
    }
}
