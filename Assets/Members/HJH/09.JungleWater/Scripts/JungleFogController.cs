using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using Members.JJH._02_Scripts.ElementsSystem;
using Members.KYR._01.Scripts.Player;
using UnityEngine;
using UnityEngine.Rendering;

namespace Assets.Members.HJH.Environment
{
    public class JungleFogController : MonoBehaviour
    {
        private const int MaxHoles = 8;

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
            Shader.SetGlobalVectorArray(HolesId, _holeData);
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
                _holes.RemoveAt(0);
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

        private void OnBeginCamera(ScriptableRenderContext context, Camera cam)
        {
            bool reveal = _player != null && cam.cameraType == CameraType.Game;
            Shader.SetGlobalVector(PlayerPosId, reveal ? (Vector4)_player.position + new Vector4(0f, 0f, 0f, 1f) : Vector4.zero);
        }
    }
}
