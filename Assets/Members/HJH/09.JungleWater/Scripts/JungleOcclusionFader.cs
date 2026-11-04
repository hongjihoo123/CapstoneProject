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
            // 나무는 안 움직이니 박스를 Scan 때 저장해 둠 (renderer.bounds 는 엔진 호출이라 매 프레임 부르면 비쌈)
            public Bounds Bounds;
            public float Alpha = 1f;
        }

        [Tooltip("스킬이 터진 자리 위 나무를 반투명하게 두는 시간 (초)")]
        [SerializeField] private float impactFadeSeconds = 2f;

        // ---- 바위 (2026-11-02): 큰 바위가 카메라를 가리면 반투명 머티리얼로 바꿔 끼움
        //  - 바위 셰이더(SG_PBR)엔 _Fade 가 없어서, Resources/HJH/RockFade/<원본>_Fade 반투명 복사본으로 교체 (HJH/Map/Rocks 메뉴가 만듦)
        //  - 큰 절벽은 바운딩 박스가 커서 상자 검사로는 늘 가린다고 나옴 → 실제 콜라이더에 광선을 쏴서 판정
        [Header("Rocks")]
        [Tooltip("이보다 작은 바위는 안 투명해짐 (m)")]
        [SerializeField] private float rockMinSize = 3f;
        [SerializeField, Range(0f, 1f)] private float rockFadedAlpha = 0.3f;

        private sealed class RockEntry
        {
            public Renderer Renderer;
            public Material[] Original;
            public Material[] Faded;
            public float Alpha = 1f;
            public bool Swapped;
            public bool Blocking;
        }

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private readonly List<RockEntry> _rocks = new();
        private readonly Dictionary<Collider, RockEntry> _rockByCollider = new();
        private readonly Dictionary<Material, Material> _fadeCopies = new();
        private readonly RaycastHit[] _rockHits = new RaycastHit[16];

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
            for (int i = _impacts.Count - 1; i >= 0; i--)
                if (now > _impacts[i].until)
                    _impacts.RemoveAt(i);
            foreach ((Vector3 point, float radius, float _) in _impacts)
                AddSight(from, point + Vector3.up, Mathf.Max(margin, radius));

            // 모든 선을 감싸는 큰 상자. 이 밖의 나무는 정밀 검사(IntersectRay, 엔진 호출)를 건너뜀
            var area = new Bounds(from, Vector3.zero);
            float widest = 0f;
            foreach ((Ray ray, float length, float width) in _sights)
            {
                area.Encapsulate(ray.GetPoint(length));
                widest = Mathf.Max(widest, width);
            }
            area.Expand(widest * 2f);

            float step = fadeSpeed * Time.unscaledDeltaTime;
            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                Entry e = _entries[i];
                bool blocking = false;
                if (area.Intersects(e.Bounds))
                foreach ((Ray ray, float length, float width) in _sights)
                {
                    Bounds b = e.Bounds;
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

                // 지워진 렌더러는 값을 바꿀 때만 확인 (null 확인도 엔진 호출이라 전부에 매 프레임 하지 않음)
                if (e.Renderer == null)
                {
                    _entries.RemoveAt(i);
                    continue;
                }

                e.Alpha = Mathf.MoveTowards(e.Alpha, goal, step);
                e.Renderer.GetPropertyBlock(_block);
                _block.SetFloat(FadeId, e.Alpha);
                e.Renderer.SetPropertyBlock(_block);
            }

            UpdateRocks(step);
        }

        // 카메라 → 플레이어(·스킬 자리) 선마다 광선 3개(가운데·양옆)를 바위 콜라이더에 쏴서 가리는 바위를 찾음
        private void UpdateRocks(float step)
        {
            if (_rocks.Count == 0)
                return;

            foreach (RockEntry rock in _rocks)
                rock.Blocking = false;

            // 카메라가 바위 속에 들어가 있어도 맞게 (메시 콜라이더 안쪽 면도 맞힘)
            bool backfaces = Physics.queriesHitBackfaces;
            Physics.queriesHitBackfaces = true;
            Vector3 side = _camera != null ? _camera.transform.right : transform.right;
            foreach ((Ray ray, float length, float width) in _sights)
            {
                Vector3 end = ray.GetPoint(length);
                MarkBlocking(ray.origin, end);
                MarkBlocking(ray.origin, end + side * width * 0.6f);
                MarkBlocking(ray.origin, end - side * width * 0.6f);
            }
            Physics.queriesHitBackfaces = backfaces;

            foreach (RockEntry rock in _rocks)
            {
                float goal = rock.Blocking ? rockFadedAlpha : 1f;
                if (Mathf.Approximately(rock.Alpha, goal) || rock.Renderer == null)
                    continue;

                rock.Alpha = Mathf.MoveTowards(rock.Alpha, goal, step);
                if (rock.Alpha >= 0.999f)
                {
                    // 다 돌아왔으면 원래 머티리얼로 (불투명 + SRP 배칭 그대로)
                    rock.Renderer.sharedMaterials = rock.Original;
                    rock.Renderer.SetPropertyBlock(null);
                    rock.Swapped = false;
                    continue;
                }

                if (!rock.Swapped)
                {
                    rock.Renderer.sharedMaterials = rock.Faded;
                    rock.Swapped = true;
                }
                _block.Clear();
                _block.SetColor(BaseColorId, new Color(1f, 1f, 1f, rock.Alpha));
                rock.Renderer.SetPropertyBlock(_block);
            }
        }

        private void MarkBlocking(Vector3 from, Vector3 to)
        {
            Vector3 dir = to - from;
            float length = dir.magnitude;
            if (length < 0.01f)
                return;
            int count = Physics.RaycastNonAlloc(from, dir / length, _rockHits, length, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (_rockByCollider.TryGetValue(_rockHits[i].collider, out RockEntry rock))
                    rock.Blocking = true;
        }

        // 큰 바위 + 반투명 복사본이 있는 머티리얼 + 콜라이더가 있으면 등록
        private bool TryAddRock(Renderer r)
        {
            if (r is not MeshRenderer || !r.TryGetComponent(out Collider collider))
                return false;
            Vector3 size = r.bounds.size;
            if (Mathf.Max(size.x, size.y, size.z) < rockMinSize)
                return false;

            Material[] original = r.sharedMaterials;
            var faded = new Material[original.Length];
            bool any = false;
            for (int i = 0; i < original.Length; i++)
            {
                faded[i] = FadeCopyOf(original[i]) ?? original[i];
                any |= faded[i] != original[i];
            }
            if (!any)
                return false;

            var rock = new RockEntry { Renderer = r, Original = original, Faded = faded };
            _rocks.Add(rock);
            _rockByCollider[collider] = rock;
            return true;
        }

        private Material FadeCopyOf(Material source)
        {
            if (source == null)
                return null;
            if (!_fadeCopies.TryGetValue(source, out Material copy))
                _fadeCopies[source] = copy = Resources.Load<Material>("HJH/RockFade/" + source.name + "_Fade");
            return copy;
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
            // 이미 아는 것: 지워졌으면 빼고, 남은 건 박스를 새로 (옮겨진 나무가 있어도 다음 Scan 에 맞춰짐)
            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                Entry e = _entries[i];
                if (e.Renderer == null)
                    _entries.RemoveAt(i);
                else
                    e.Bounds = e.Renderer.bounds;
            }
            _known.RemoveWhere(r => r == null);

            foreach (Renderer r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (_known.Contains(r) || _rockRejected.Contains(r))
                    continue;
                if (IsNeverFade(r))
                {
                    _rockRejected.Add(r);   // 바닥 취급 → 다음 Scan 때 다시 안 봄
                    continue;
                }
                if (HasFade(r))
                {
                    _known.Add(r);
                    _entries.Add(new Entry { Renderer = r, Bounds = r.bounds });
                }
                else if (!_rockRejected.Contains(r))
                {
                    if (TryAddRock(r))
                        _known.Add(r);
                    else
                        _rockRejected.Add(r);   // 바위 아님 → 다음 Scan 때 다시 안 봄
                }
            }
        }

        private readonly HashSet<Renderer> _rockRejected = new();

        [Tooltip("재질 이름에 이 글자가 들어가면 절대 투명해지지 않음 (바닥 역할: 뿌리 둔덕 MagrobeWood = M_Roots_Toon 등). 쉼표로 여러 개")]
        [SerializeField] private string neverFadeMaterials = "Roots";

        private bool IsNeverFade(Renderer r)
        {
            Material m = r.sharedMaterial;
            if (m == null || string.IsNullOrEmpty(neverFadeMaterials))
                return false;
            foreach (string key in neverFadeMaterials.Split(','))
            {
                string k = key.Trim();
                if (k.Length > 0 && m.name.Contains(k))
                    return true;
            }
            return false;
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

            foreach (RockEntry rock in _rocks)
            {
                if (rock.Renderer == null || !rock.Swapped)
                    continue;
                rock.Renderer.sharedMaterials = rock.Original;
                rock.Renderer.SetPropertyBlock(null);
                rock.Swapped = false;
                rock.Alpha = 1f;
            }
        }
    }
}
