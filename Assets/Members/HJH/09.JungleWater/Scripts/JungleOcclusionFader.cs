using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using Members.KYR._01.Scripts.Player;
using UnityEngine;

namespace Assets.Members.HJH.Environment
{
    // 카메라와 플레이어 사이, 그리고 방금 스킬이 터진 자리를 가리는 나무·잎을 반투명하게 (캐릭터·이펙트가 안 가려지게).
    //  - 셰이더에 _Fade 가 있는 머티리얼(잎 SG_JungleLeaf, 줄기 SG_JungleBark)을 쓰는 렌더러만 대상
    //  - 콜라이더가 필요 없다: 렌더러 바운딩 박스가 '카메라 → 플레이어' 선분에 걸리는지만 본다
    //  - 셰이더는 디더링으로 반투명 처리 → 투명 셰이더처럼 그리는 순서 문제가 없음
    // 씬에 _Fade 머티리얼이 있으면 메인 카메라에 자동으로 붙는다.
    [DisallowMultipleComponent]
    public class JungleOcclusionFader : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [Tooltip("가릴 때 남는 정도 (0 = 완전히 사라짐)")]
        [SerializeField, Range(0f, 1f)] private float fadedAlpha = 0.3f;
        [SerializeField] private float fadeSpeed = 5f;
        [Tooltip("캐릭터 몸통 높이 (선분 끝)")]
        [SerializeField] private float targetHeight = 1.2f;
        [Tooltip("이만큼 넓게 가린다고 본다 (m)")]
        [SerializeField] private float margin = 1.2f;
        [Tooltip("새로 생긴 나무를 찾는 간격 (초)")]
        [SerializeField] private float rescanInterval = 3f;

        private static readonly int FadeId = Shader.PropertyToID("_Fade");

        private sealed class Entry
        {
            public Renderer Renderer;
            public float Alpha = 1f;
        }

        [Tooltip("스킬이 터진 자리 위 나무를 반투명하게 두는 시간 (초)")]
        [SerializeField] private float impactFadeSeconds = 2f;

        private readonly List<(Ray ray, float length, float width)> _sights = new();
        private readonly List<(Vector3 point, float radius, float until)> _impacts = new();
        private readonly List<Entry> _entries = new();
        private readonly HashSet<Renderer> _known = new();
        private MaterialPropertyBlock _block;
        private Camera _camera;
        private float _nextScan;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            Camera cam = Camera.main;
            if (cam == null || cam.GetComponent<JungleOcclusionFader>() != null)
                return;

            foreach (Renderer r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (HasFade(r))
                {
                    cam.gameObject.AddComponent<JungleOcclusionFader>();
                    return;
                }
            }
        }

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            _block = new MaterialPropertyBlock();
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                PlayerAgent player = FindFirstObjectByType<PlayerAgent>();
                if (player == null)
                    return;
                target = player.transform;
            }

            if (Time.unscaledTime >= _nextScan)
            {
                _nextScan = Time.unscaledTime + rescanInterval;
                Scan();
            }

            Vector3 from = _camera != null ? _camera.transform.position : transform.position;

            // 가리는지 볼 선들: 카메라 → 플레이어, 카메라 → 방금 스킬이 터진 자리들
            _sights.Clear();
            AddSight(from, target.position + Vector3.up * targetHeight, margin);
            float now = Time.unscaledTime;
            _impacts.RemoveAll(i => now > i.until);
            foreach ((Vector3 point, float radius, float _) in _impacts)
                AddSight(from, point + Vector3.up, Mathf.Max(margin, radius));

            float step = fadeSpeed * Time.unscaledDeltaTime;
            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                Entry e = _entries[i];
                if (e.Renderer == null)
                {
                    _entries.RemoveAt(i);
                    continue;
                }

                bool blocking = false;
                foreach ((Ray ray, float length, float width) in _sights)
                {
                    Bounds b = e.Renderer.bounds;
                    b.Expand(width * 2f);
                    if (b.IntersectRay(ray, out float hitDistance) && hitDistance < length)
                    {
                        blocking = true;
                        break;
                    }
                }

                float goal = blocking ? fadedAlpha : 1f;
                if (Mathf.Approximately(e.Alpha, goal))
                    continue;

                e.Alpha = Mathf.MoveTowards(e.Alpha, goal, step);
                e.Renderer.GetPropertyBlock(_block);
                _block.SetFloat(FadeId, e.Alpha);
                e.Renderer.SetPropertyBlock(_block);
            }
        }

        private void AddSight(Vector3 from, Vector3 to, float width)
        {
            Vector3 segment = to - from;
            float length = segment.magnitude;
            if (length > 0.01f)
                _sights.Add((new Ray(from, segment / length), length, width));
        }

        // 스킬이 터진 자리는 잠깐 동안 그 위 나무·잎을 반투명하게 (이펙트가 나무에 안 가려지게)
        private void OnEnable() => SkillImpacts.Impacted += OnSkillImpact;
        private void OnSkillImpact(Vector3 position, float radius) =>
            _impacts.Add((position, radius, Time.unscaledTime + impactFadeSeconds));

        private void Scan()
        {
            foreach (Renderer r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (_known.Contains(r) || !HasFade(r))
                    continue;
                _known.Add(r);
                _entries.Add(new Entry { Renderer = r });
            }
        }

        private static bool HasFade(Renderer r)
        {
            Material m = r.sharedMaterial;
            return m != null && m.HasProperty(FadeId);
        }

        private void OnDisable()
        {
            SkillImpacts.Impacted -= OnSkillImpact;
            // 꺼질 때 반투명으로 남지 않게 되돌림 (플레이 중 코드 다시 불러오기 뒤엔 블록이 비어 있을 수 있음)
            _block ??= new MaterialPropertyBlock();
            foreach (Entry e in _entries)
            {
                if (e.Renderer == null)
                    continue;
                e.Renderer.GetPropertyBlock(_block);
                _block.SetFloat(FadeId, 1f);
                e.Renderer.SetPropertyBlock(_block);
                e.Alpha = 1f;
            }
        }
    }
}
