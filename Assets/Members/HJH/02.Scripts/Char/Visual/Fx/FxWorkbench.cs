using System.Collections.Generic;
using System.Reflection;
using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.ElementSkill;
using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Assets.Members.HJH._02.Scripts.Char.Visual
{
    // 이펙트 꾸미기 작업대 (테스트 씬 전용). 빈 오브젝트에 붙이고 Skill 칸에 원소 스킬 에셋 하나를 넣은 뒤 Play.
    //
    //  [스킬 파트] 이 오브젝트 자리 = 시전자(캡슐). 스킬의 "진짜 코드"를 그대로 돌려서 게임과 똑같이 나감
    //             (시전 위치, 투사체, 선 따라 여러 번 터짐, 돌진, 장판 틱... 전부 스킬 에셋 그대로)
    //  [적 파트]  옆에 따로 서 있는 허수아비들. 스킬이 맞히면 맞은 이펙트(hitFx)·CC 표시(statusFx)·데미지 숫자가 여기에 뜸
    //             (판정은 스킬 파트의 조준 지점에서 함 → 투사체가 멈추고 터지는 자리는 게임과 같음)
    //
    //  1. Play하면 그 스킬이 쓰는 파티클 프리팹이 전부 "[작업대 파티클]" 아래에 수정용 사본으로 채워짐
    //  2. 스킬이 이펙트를 낼 때 프리팹 대신 이 사본을 씀 → Play 중에 사본을 고치면 다음 시전에 바로 보임
    //  3. 고친 건 "프리팹에 저장" 버튼 또는 Play를 끌 때 원래 프리팹 파일에 저장됨 (Save On Stop)
    //  4. Play 중에 Skill 칸을 다른 스킬로 바꾸면: (바뀐 건 저장하고) 기존 사본은 빠지고 새 스킬 것으로 다시 채워짐
    //
    //  F5 = 스킬 쓰기 · F6 = 자동 반복 켜기/끄기 (게임 조작키와 안 겹치게 F키)
    public class FxWorkbench : MonoBehaviour
    {
        // 스킬 하나가 쓰는 프리팹 하나 = Part. 같은 프리팹을 여러 칸이 쓰면 사본은 하나 (예: 예고와 속박이 같은 프리팹)
        public sealed class Part
        {
            public string Slots;
            public GameObject Prefab;
            public GameObject Copy;
            public string Snapshot; // 마지막으로 저장/불러온 상태. 지금과 다르면 "바뀜"
            public double CheckedAt;  // 바뀜 검사 캐시 (인스펙터가 매 프레임 묻지 않게)
            public bool WasChanged;

            // 보기용 끄기 (재생할 때만 안 보이게. 사본·프리팹은 안 바뀜 → 저장 안 됨)
            public bool Hidden;
            public readonly HashSet<Transform> HiddenLayers = new();
        }

        [SerializeField, Tooltip("05.Data/Skill/Element 의 스킬 에셋. Play 중에 바꾸면 파티클이 그 스킬 것으로 다시 채워짐")]
        private ElementSkillBase skill;

        [Header("스킬 파트")]
        [SerializeField, Tooltip("조준 거리 = 커서가 있는 곳. 허수아비 판정 자리도 여기")] private float aimDistance = 6f;
        [SerializeField, Tooltip("자동 반복 간격(초)")] private float interval = 2.5f;
        [SerializeField, Tooltip("1보다 크면 천천히. 2 = 절반 속도")] private float slowMotion = 1f;
        [SerializeField] private bool autoFire = true;
        [SerializeField] private Key fireKey = Key.F5;
        [SerializeField] private Key autoKey = Key.F6;

        [Header("적 파트")]
        [SerializeField, Range(1, 5), Tooltip("허수아비 수")] private int enemyCount = 3;
        [SerializeField, Tooltip("적 파트 위치 (작업대 기준: x = 오른쪽, z = 앞)")] private Vector3 enemyArea = new(10f, 0f, 4f);
        [SerializeField, Tooltip("적 파트에서 허수아비 사이 간격")] private float enemySpacing = 2.2f;
        [SerializeField, Tooltip("스킬 파트 조준 지점에서 판정용 허수아비 사이 간격")] private float hitSpread = 0.7f;

        [Header("저장")]
        [SerializeField, Tooltip("Play를 끄거나 스킬을 바꿀 때, 바뀐 파티클을 원래 프리팹에 자동 저장")]
        private bool saveOnStop = true;

        private static readonly string[] SlotNames = { "castFx", "auraFx", "selfFx", "windupFx", "travelFx", "impactFx", "hitFx", "statusFx" };
        private const BindingFlags AnyField = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        private readonly List<Part> _parts = new();
        private readonly List<WorkbenchDummy> _dummies = new();
        private ElementSkillBase _loaded;
        private Transform _holder;
        private Transform _caster;
        private Transform _stage;
        private WorkbenchSkillContext _context;
        private ISkillExecution _execution;
        private float _castTime;
        private float _next;
        private bool _slowed;
        private GUIStyle _style;

        public IReadOnlyList<Part> Parts => _parts;
        public ElementSkillBase LoadedSkill => _loaded;

        private Vector3 Forward
        {
            get
            {
                Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
                return forward.sqrMagnitude < 0.001f ? Vector3.forward : forward.normalized;
            }
        }

        private Vector3 Right => Vector3.Cross(Vector3.up, Forward);
        private Vector3 AimPoint => transform.position + Forward * aimDistance;
        private Vector3 EnemyCenter => transform.position + Right * enemyArea.x + Vector3.up * enemyArea.y + Forward * enemyArea.z;

        // ---------- 시작 / 끝 ----------
        private void Start() => BuildStage();

        private void OnEnable()
        {
            FxPrefabInstance.SourceOverride = EditedCopyOf;
            FxPrefabInstance.OverrideSpawned = HideMutedLayers;
#if UNITY_EDITOR
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
#endif
        }

        private void OnDisable()
        {
            if (FxPrefabInstance.SourceOverride == EditedCopyOf)
                FxPrefabInstance.SourceOverride = null;
            if (FxPrefabInstance.OverrideSpawned == HideMutedLayers)
                FxPrefabInstance.OverrideSpawned = null;
#if UNITY_EDITOR
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
#endif
            if (_slowed)
                Time.timeScale = 1f;
            _slowed = false;
        }

#if UNITY_EDITOR
        private void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingPlayMode && saveOnStop)
                FxWorkbenchSaver.SaveChanged(this);
        }
#endif

        // 스킬이 이펙트를 낼 때 "이 프리팹 대신 수정용 사본을 써" (작업대가 불러온 프리팹만)
        private GameObject EditedCopyOf(GameObject prefab)
        {
            foreach (Part part in _parts)
                if (part.Prefab == prefab)
                    return part.Copy;
            return null;
        }

        // ---------- 보기용 켜기/끄기 ----------
        // 재생용 복사본의 렌더러만 켜고 끔 (사본·프리팹은 그대로 → ● 안 생기고 저장 안 됨).
        // 화면에 떠 있는 복사본에도 매 프레임 적용해서, 체크를 바꾸면 바로 보임.
        private sealed class Live
        {
            public Part Part;
            public GameObject Root;
            public Renderer[] Renderers;
            public Transform[] Layers;   // 각 렌더러가 사본의 어느 오브젝트(레이어)에 해당하는지
            public bool[] WasEnabled;    // 프리팹에서 원래 꺼져 있던 렌더러는 계속 꺼둠
        }

        private readonly List<Live> _live = new();

        private void HideMutedLayers(GameObject source, GameObject spawned)
        {
            Part part = _parts.Find(p => p.Copy == source);
            if (part == null)
                return;

            Renderer[] renderers = spawned.GetComponentsInChildren<Renderer>(true);
            var live = new Live
            {
                Part = part,
                Root = spawned,
                Renderers = renderers,
                Layers = new Transform[renderers.Length],
                WasEnabled = new bool[renderers.Length]
            };
            for (int i = 0; i < renderers.Length; i++)
            {
                live.Layers[i] = Twin(renderers[i].transform, spawned.transform, source.transform);
                live.WasEnabled[i] = renderers[i].enabled;
            }

            _live.Add(live);
            Apply(live);
        }

        private void LateUpdate()
        {
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                if (_live[i].Root == null)
                    _live.RemoveAt(i);
                else
                    Apply(_live[i]);
            }
        }

        private static void Apply(Live live)
        {
            Part part = live.Part;
            for (int i = 0; i < live.Renderers.Length; i++)
            {
                Renderer renderer = live.Renderers[i];
                if (renderer == null)
                    continue;
                bool visible = !part.Hidden && (live.Layers[i] == null || !part.HiddenLayers.Contains(live.Layers[i]));
                renderer.enabled = live.WasEnabled[i] && visible;
            }
        }

        // from 쪽 node 와 같은 자리(형제 순서 경로)에 있는 to 쪽 오브젝트
        private static Transform Twin(Transform node, Transform fromRoot, Transform toRoot)
        {
            if (node == null)
                return null;
            var path = new List<int>();
            for (Transform t = node; t != fromRoot; t = t.parent)
            {
                if (t == null)
                    return null;
                path.Add(t.GetSiblingIndex());
            }

            Transform twin = toRoot;
            for (int i = path.Count - 1; i >= 0; i--)
            {
                if (path[i] >= twin.childCount)
                    return null;
                twin = twin.GetChild(path[i]);
            }
            return twin;
        }

        public void ShowAll()
        {
            foreach (Part part in _parts)
            {
                part.Hidden = false;
                part.HiddenLayers.Clear();
            }
        }

        // 이 칸만 보이게 (다른 칸 전부 끔)
        public void SoloPart(Part solo)
        {
            foreach (Part part in _parts)
                part.Hidden = part != solo;
            solo.HiddenLayers.Clear();
        }

        // 이 칸에서 이 레이어(와 그 자식들)만 보이게
        public void SoloLayer(Part part, Transform layer)
        {
            SoloPart(part);
            foreach (Transform other in Layers(part))
            {
                if (other != layer && !other.IsChildOf(layer))
                    part.HiddenLayers.Add(other);
            }
        }

        // 여러 칸에 걸쳐 고른 레이어들만 보이게 (검색 결과만 보기)
        public void SoloLayers(IEnumerable<(Part part, Transform layer)> keep)
        {
            var keepSet = new HashSet<Transform>();
            var keepParts = new HashSet<Part>();
            foreach ((Part part, Transform layer) in keep)
            {
                keepSet.Add(layer);
                keepParts.Add(part);
            }

            foreach (Part part in _parts)
            {
                part.Hidden = !keepParts.Contains(part);
                part.HiddenLayers.Clear();
                if (part.Hidden)
                    continue;
                foreach (Transform layer in Layers(part))
                {
                    if (!keepSet.Contains(layer))
                        part.HiddenLayers.Add(layer);
                }
            }
        }

        public static void SetAllLayers(Part part, bool visible)
        {
            part.HiddenLayers.Clear();
            if (!visible)
                foreach (Transform layer in Layers(part))
                    part.HiddenLayers.Add(layer);
        }

        public static void InvertLayers(Part part)
        {
            foreach (Transform layer in Layers(part))
            {
                if (!part.HiddenLayers.Remove(layer))
                    part.HiddenLayers.Add(layer);
            }
        }

        // 파티클·트레일이 붙은 오브젝트들 = 레이어 목록 (인스펙터에 보여줄 순서)
        public static List<Transform> Layers(Part part)
        {
            var layers = new List<Transform>();
            if (part.Copy == null)
                return layers;
            foreach (Transform node in part.Copy.GetComponentsInChildren<Transform>(true))
            {
                if (node.TryGetComponent(out ParticleSystem _) || node.TryGetComponent(out TrailRenderer _))
                    layers.Add(node);
            }
            return layers;
        }

        // 시전자 캡슐 + 허수아비(판정용은 스킬 파트, 몸은 적 파트)
        private void BuildStage()
        {
            _stage = new GameObject("[작업대 무대]").transform;
            _stage.SetParent(transform, false);

            _caster = Capsule("시전자", _stage, new Color(0.35f, 0.6f, 1f)).transform;

            Vector3 right = Right;
            for (int i = 0; i < enemyCount; i++)
            {
                float side = (i - (enemyCount - 1) * 0.5f);
                GameObject body = Capsule($"허수아비 {i + 1}", _stage, new Color(0.85f, 0.45f, 0.4f));
                body.transform.position = EnemyCenter + right * side * enemySpacing;
                body.transform.rotation = Quaternion.LookRotation(-Forward);

                var hitPoint = new GameObject($"허수아비 {i + 1} 판정 자리").transform;
                hitPoint.SetParent(_stage, false);
                hitPoint.position = AimPoint + right * side * hitSpread;
                var dummy = hitPoint.gameObject.AddComponent<WorkbenchDummy>();
                dummy.Setup(body.transform);
                _dummies.Add(dummy);
            }

            _context = new WorkbenchSkillContext(_caster, Forward, aimDistance, _dummies);
            ResetCaster();
        }

        // 발 위치(= 게임 캐릭터의 위치 기준)에 빈 오브젝트, 그 1m 위에 보이는 캡슐
        private static GameObject Capsule(string name, Transform parent, Color color)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);

            GameObject shape = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            shape.name = "모양";
            shape.transform.SetParent(root.transform, false);
            shape.transform.localPosition = Vector3.up;
            shape.transform.localScale = new Vector3(0.8f, 1f, 0.8f);
            Destroy(shape.GetComponent<Collider>());
            shape.GetComponent<Renderer>().material.color = color;
            return root;
        }

        private void ResetCaster() =>
            _caster.SetPositionAndRotation(transform.position, Quaternion.LookRotation(Forward));

        // ---------- 매 프레임 ----------
        private void Update()
        {
            if (skill != _loaded)
                Load(skill, saveChanges: saveOnStop);

            if (slowMotion > 1f)
            {
                Time.timeScale = 1f / slowMotion;
                _slowed = true;
            }
            else if (_slowed)
            {
                Time.timeScale = 1f;
                _slowed = false;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard[fireKey].wasPressedThisFrame)
                    PlayNow();
                if (keyboard[autoKey].wasPressedThisFrame)
                    autoFire = !autoFire;
            }

            if (autoFire && Time.time >= _next)
                PlayNow();

            // 게임의 스킬 상태와 같은 순서: 지속 시간 동안 Tick, 끝나면 End
            _context?.Tick(Time.deltaTime);
            if (_execution != null && _loaded != null)
            {
                float elapsed = Time.time - _castTime;
                if (elapsed >= _loaded.Duration)
                    EndExecution();
                else
                    _execution.Tick(elapsed);
            }
        }

        // ---------- 스킬 쓰기 (진짜 스킬 코드) ----------
        public void PlayNow()
        {
            _next = Time.time + interval;
            if (_loaded == null || _context == null || !isActiveAndEnabled)
                return;

            EndExecution();
            _context.CancelDash();
            ResetCaster();
            foreach (WorkbenchDummy dummy in _dummies)
                dummy.ResetState();

            _context.ActiveSkill = _loaded;
            _castTime = Time.time;
            _execution = _loaded.Begin(_context);
        }

        private void EndExecution()
        {
            if (_execution == null)
                return;
            ISkillExecution ending = _execution;
            _execution = null;
            ending.End();
            if (_context.ActiveSkill == _loaded)
                _context.ActiveSkill = null;
        }

        // ---------- 불러오기 / 비우기 ----------
        // 지금 사본을 버리고 (필요하면 먼저 저장하고) next 스킬의 파티클로 다시 채움
        public void Load(ElementSkillBase next, bool saveChanges)
        {
#if UNITY_EDITOR
            if (saveChanges)
                FxWorkbenchSaver.SaveChanged(this);
#endif
            EndExecution();
            _parts.Clear();
            if (_holder != null)
                Destroy(_holder.gameObject);
            _holder = null;
            _loaded = next;
            if (next == null)
                return;

            // 꺼둔 부모 아래에 만들면 사본은 재생되지 않고 값만 들고 있음 (재생은 이걸 복사해서)
            _holder = new GameObject($"[작업대 파티클] {next.name}").transform;
            _holder.SetParent(transform, false);
            _holder.gameObject.SetActive(false);

            foreach (string slotName in SlotNames)
            {
                FxSlot slot = Slot(next, slotName);
                if (slot == null || slot.prefab == null)
                    continue;

                Part part = _parts.Find(p => p.Prefab == slot.prefab);
                if (part != null)
                {
                    part.Slots += ", " + slotName;
                }
                else
                {
                    GameObject copy = Instantiate(slot.prefab, _holder);
                    _parts.Add(new Part { Slots = slotName, Prefab = slot.prefab, Copy = copy });
                }
            }

            foreach (Part part in _parts)
            {
                part.Copy.name = $"{part.Prefab.name}   ← {part.Slots}";
#if UNITY_EDITOR
                part.Snapshot = FxWorkbenchSaver.Snapshot(part.Copy);
#endif
            }

            _next = Time.time + 0.3f;
        }

        // 저장 안 하고 프리팹 상태로 되돌리기
        public void Revert() => Load(_loaded, saveChanges: false);

        // travelFx 같은 칸은 스킬 모양 클래스 안에 private로 있어서 이름으로 꺼냄 (테스트 도구라 괜찮음)
        private static FxSlot Slot(ElementSkillBase owner, string field)
        {
            for (System.Type type = owner.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo info = type.GetField(field, AnyField | BindingFlags.DeclaredOnly);
                if (info != null)
                    return info.GetValue(owner) as FxSlot;
            }
            return null;
        }

        // ---------- 보이기 ----------
        private void OnDrawGizmos()
        {
            Vector3 forward = Forward, right = Right;
            Gizmos.color = new Color(0.35f, 0.6f, 1f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up, 0.5f);
            Gizmos.DrawLine(transform.position, AimPoint);

            Gizmos.color = new Color(1f, 0.5f, 0.4f);
            for (int i = 0; i < enemyCount; i++)
            {
                float side = (i - (enemyCount - 1) * 0.5f);
                Gizmos.DrawWireSphere(AimPoint + right * side * hitSpread + Vector3.up * 0.5f, 0.45f);
                Gizmos.DrawWireCube(EnemyCenter + right * side * enemySpacing + Vector3.up, new Vector3(0.8f, 2f, 0.8f));
            }
            Gizmos.DrawLine(AimPoint, EnemyCenter);
        }

        private void OnGUI()
        {
            _style ??= new GUIStyle(GUI.skin.label) { fontSize = 18, normal = { textColor = Color.white } };
            string name = _loaded != null ? _loaded.name : "(Skill 칸 비어 있음)";
            GUI.Label(new Rect(16, 16, 1100, 30),
                $"작업대: {name}   ({fireKey} 스킬 쓰기 · {autoKey} 자동 {(autoFire ? "ON" : "OFF")})", _style);

            Camera view = Camera.main;
            if (view == null)
                return;

            // 허수아비 머리 위에 지금 걸린 CC
            foreach (WorkbenchDummy dummy in _dummies)
            {
                if (dummy == null || dummy.Body == null)
                    continue;
                string status = dummy.Status;
                if (string.IsNullOrEmpty(status))
                    continue;
                Vector3 screen = view.WorldToScreenPoint(dummy.Body.position + Vector3.up * 1.6f);
                if (screen.z > 0f)
                    GUI.Label(new Rect(screen.x - 80f, Screen.height - screen.y - 12f, 260f, 28f), status, _style);
            }
        }
    }
}
