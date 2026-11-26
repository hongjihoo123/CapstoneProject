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
    public class FxWorkbench : MonoBehaviour
    {
        public sealed class Part
        {
            public string Slots;
            public GameObject Prefab;
            public GameObject Copy;
            public string Snapshot;
            public double CheckedAt;
            public bool WasChanged;

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

        private static readonly string[] SlotNames = { "castFx", "auraFx", "selfFx", "windupFx", "travelFx", "streamFx", "zoneFx", "barrierFx", "blockFx", "impactFx", "hitFx", "statusFx" };
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

        private GameObject EditedCopyOf(GameObject prefab)
        {
            foreach (Part part in _parts)
                if (part.Prefab == prefab)
                    return part.Copy;
            return null;
        }

        private sealed class Live
        {
            public Part Part;
            public GameObject Root;
            public Renderer[] Renderers;
            public Transform[] Layers;
            public bool[] WasEnabled;
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

        public void SoloPart(Part solo)
        {
            foreach (Part part in _parts)
                part.Hidden = part != solo;
            solo.HiddenLayers.Clear();
        }

        public void SoloLayer(Part part, Transform layer)
        {
            SoloPart(part);
            foreach (Transform other in Layers(part))
            {
                if (other != layer && !other.IsChildOf(layer))
                    part.HiddenLayers.Add(other);
            }
        }

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

        public void Revert() => Load(_loaded, saveChanges: false);

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
