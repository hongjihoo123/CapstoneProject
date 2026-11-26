using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using Assets.Members.HJH._02.Scripts.Hud;
using Assets.Members.HJH._02.Scripts.Quest;
using Assets.Members.HJH._02.Scripts.UI;
using Assets.Members.JJH._02_Scripts.Test;
using Members.JJH._02_Scripts.Agents.Enemies;
using Members.KYR._01.Scripts.Player;
using Members.KYR._01.Scripts.Spawner;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace Assets.Members.HJH._02.Scripts.Tutorial
{
    public sealed class TutorialContext
    {
        public readonly TutorialInput Input = new();
        public int StepIndex { get; internal set; }
        public int StepCount { get; internal set; }
        public TutorialView View { get; internal set; }

        private readonly Dictionary<SkillSlotId, int> _skillUses = new();
        private readonly List<ITutorialAnchors> _anchorSources = new();
        private readonly List<Behaviour> _heldSpawners = new();
        private PlayerAgent _player;
        private PlayerHudSource _hud;
        private RoomClearTracker _tracker;
        private ITutorialPickScreen _pick;
        private Vector3 _lastPlayerPos;
        private bool _hasLastPos;

        public int BasicAttacks { get; private set; }
        public int CombosLanded { get; private set; }
        public float MovedDistance { get; private set; }

        public void DebugCountBasicAttack() => BasicAttacks++;

        public int SkillUses(SkillSlotId slot) => _skillUses.TryGetValue(slot, out int n) ? n : 0;

        public PlayerAgent Player
        {
            get
            {
                if (_player == null)
                    _player = Object.FindFirstObjectByType<PlayerAgent>();
                return _player;
            }
        }

        public PlayerHudSource Hud
        {
            get
            {
                if (_hud == null && Player != null)
                {
                    _hud = PlayerHudSource.Instance;
                    if (_hud != null)
                    {
                        _hud.SkillUsed += (slot, _) => _skillUses[slot] = SkillUses(slot) + 1;
                        _hud.ComboLanded += _ => CombosLanded++;
                    }
                }
                return _hud;
            }
        }

        public RoomClearTracker Tracker
        {
            get
            {
                if (_tracker == null)
                    _tracker = Object.FindFirstObjectByType<RoomClearTracker>();
                return _tracker;
            }
        }

        public ITutorialPickScreen Pick
        {
            get
            {
                if (_pick == null)
                    _pick = FindInterface<ITutorialPickScreen>();
                return _pick;
            }
        }

        public bool HasSkill(SkillSlotId slot) => Player != null && Player.SkillFsm != null && Player.SkillFsm.GetSkill(slot) != null;

        public void RefreshCooldowns()
        {
            SkillStateModule skills = Player != null ? Player.SkillFsm : null;
            if (skills == null)
                return;

            foreach (SkillSlotId slot in SkillSlots.All)
            {
                SkillData skill = skills.GetSkill(slot);
                if (skill == null || skills.AnimBlendIndex == (int)slot)
                    continue;
                if (skills.GetCooldownRemaining(slot) > 0f || skills.GetCharges(slot) < skills.GetMaxCharges(slot) || skills.GetLockRemaining(slot) > 0f)
                    skills.Equip(slot, skill);
            }
        }

        public bool TryGetSlotFor(global::Members.JJH._02_Scripts.ElementsSystem.ElementType element, out SkillSlotId found)
        {
            SkillStateModule skills = Player != null ? Player.SkillFsm : null;
            foreach (SkillSlotId slot in SkillSlots.All)
            {
                SkillData skill = skills != null ? skills.GetSkill(slot) : null;
                if (skill != null && skill.TryGetElement(out var e) && e.Equals(element))
                {
                    found = slot;
                    return true;
                }
            }
            found = default;
            return false;
        }

        internal void Tick(bool paused)
        {
            _ = Hud;
            Input.Tick();

            PlayerAgent player = Player;
            if (player == null)
                return;

            Vector3 pos = player.transform.position;
            if (!paused)
            {
                if (_hasLastPos)
                {
                    Vector3 delta = pos - _lastPlayerPos;
                    delta.y = 0f;
                    if (delta.magnitude < 1.5f)
                        MovedDistance += delta.magnitude;
                }

                if (player.Input.FirePressed)
                    BasicAttacks++;
            }

            _lastPlayerPos = pos;
            _hasLastPos = true;
        }

        public bool TryGetAnchor(string id, out RectTransform rect)
        {
            rect = null;
            if (string.IsNullOrEmpty(id))
                return false;

            if (_anchorSources.Count == 0)
            {
                foreach (MonoBehaviour behaviour in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                {
                    if (behaviour is ITutorialAnchors source)
                        _anchorSources.Add(source);
                }
            }

            foreach (ITutorialAnchors source in _anchorSources)
            {
                if (source is Object unityObject && unityObject == null)
                    continue;
                if (source.TryGetAnchor(id, out rect) && rect != null)
                    return true;
            }

            return TutorialAnchors.TryFindHud(id, out rect);
        }

        public AbstractEnemy SpawnTargetAt(GameObject prefab, Vector3 position)
        {
            if (prefab == null)
            {
                Debug.LogWarning("[Tutorial] 연습용 적 프리팹이 없어 적 없이 진행합니다.");
                return null;
            }

            if (NavMesh.SamplePosition(position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                position = hit.position;
            Vector3 face = Player != null ? Player.transform.position - position : Vector3.back;
            face.y = 0f;
            Quaternion rotation = face.sqrMagnitude > 0.01f ? Quaternion.LookRotation(face) : Quaternion.identity;

            GameObject go = Object.Instantiate(prefab, position, rotation);
            go.name = "TutorialTarget_" + prefab.name;
            go.AddComponent<RoomClearIgnore>();
            var enemy = go.GetComponentInChildren<AbstractEnemy>();
            if (enemy != null)
                go.AddComponent<TutorialStationary>().Enemy = enemy;
            return enemy;
        }

        public AbstractEnemy SpawnTarget(GameObject prefab, float distance, bool stationary)
        {
            PlayerAgent player = Player;
            if (prefab == null || player == null)
            {
                Debug.LogWarning("[Tutorial] 연습용 적 프리팹이 없어 적 없이 진행합니다 (Tutorial_Stage 에셋의 단계에서 지정).");
                return null;
            }

            Vector3 pos = PickTargetSpot(player.transform.position, distance, out Vector3 forward);

            GameObject go = Object.Instantiate(prefab, pos, Quaternion.LookRotation(-forward, Vector3.up));
            go.name = "TutorialTarget_" + prefab.name;
            go.AddComponent<RoomClearIgnore>();
            var enemy = go.GetComponentInChildren<AbstractEnemy>();
            if (enemy != null && stationary)
                go.AddComponent<TutorialStationary>().Enemy = enemy;
            return enemy;
        }

        private static Vector3 PickTargetSpot(Vector3 origin, float distance, out Vector3 direction)
        {
            Camera cam = Camera.main;
            Vector3 right = cam != null ? cam.transform.right : Vector3.right;
            Vector3 up = cam != null ? cam.transform.forward : Vector3.forward;
            right.y = 0f;
            up.y = 0f;
            right = right.sqrMagnitude > 0.01f ? right.normalized : Vector3.right;
            up = up.sqrMagnitude > 0.01f ? up.normalized : Vector3.forward;

            Vector3[] candidates = { right, -right, up, (right + up).normalized, (-right + up).normalized, -up };
            bool hasOrigin = NavMesh.SamplePosition(origin, out NavMeshHit from, 2f, NavMesh.AllAreas);
            foreach (float scale in new[] { 1f, 0.7f })
            {
                foreach (Vector3 dir in candidates)
                {
                    Vector3 want = origin + dir * distance * scale;
                    if (!NavMesh.SamplePosition(want, out NavMeshHit hit, 1.5f, NavMesh.AllAreas))
                        continue;
                    if (hasOrigin && NavMesh.Raycast(from.position, hit.position, out _, NavMesh.AllAreas))
                        continue;
                    direction = dir;
                    return hit.position;
                }
            }

            direction = right;
            Vector3 fallback = origin + right * distance;
            return NavMesh.SamplePosition(fallback, out NavMeshHit any, 4f, NavMesh.AllAreas) ? any.position : fallback;
        }

        public void HoldStageSpawners(bool hold)
        {
            if (hold)
            {
                foreach (TestEnemySpawner spawner in Object.FindObjectsByType<TestEnemySpawner>(FindObjectsSortMode.None))
                    Hold(spawner);
                foreach (EnemySpawner spawner in Object.FindObjectsByType<EnemySpawner>(FindObjectsSortMode.None))
                    Hold(spawner);
                return;
            }

            foreach (Behaviour spawner in _heldSpawners)
            {
                if (spawner != null)
                    spawner.enabled = true;
            }
            _heldSpawners.Clear();
            HoldSceneEnemies(false);
        }

        private readonly List<(AbstractEnemy enemy, Unity.Behavior.BehaviorGraphAgent agent, NavMeshAgent nav)> _heldEnemies = new();

        public void HoldSceneEnemies(bool hold)
        {
            if (hold)
            {
                foreach (AbstractEnemy enemy in Object.FindObjectsByType<AbstractEnemy>(FindObjectsSortMode.None))
                {
                    if (enemy == null || !enemy.IsAlive || enemy.GetComponentInParent<RoomClearIgnore>() != null)
                        continue;
                    var agent = enemy.GetComponent<Unity.Behavior.BehaviorGraphAgent>();
                    if (agent == null || !agent.enabled)
                        continue;

                    agent.enabled = false;
                    enemy.SkillModule?.CancelAllSkills();
                    var nav = enemy.GetComponent<NavMeshAgent>();
                    if (nav != null && nav.isActiveAndEnabled && nav.isOnNavMesh)
                        nav.isStopped = true;
                    _heldEnemies.Add((enemy, agent, nav));
                }
                if (_heldEnemies.Count > 0)
                    Debug.Log($"[Tutorial] 연습 동안 씬 적 {_heldEnemies.Count}마리 멈춤");
                return;
            }

            foreach ((AbstractEnemy enemy, Unity.Behavior.BehaviorGraphAgent agent, NavMeshAgent nav) in _heldEnemies)
            {
                if (enemy == null)
                    continue;
                if (agent != null)
                    agent.enabled = true;
                if (nav != null && nav.isActiveAndEnabled && nav.isOnNavMesh)
                    nav.isStopped = false;
            }
            _heldEnemies.Clear();
        }

        private void Hold(Behaviour spawner)
        {
            if (!spawner.enabled)
                return;
            spawner.enabled = false;
            _heldSpawners.Add(spawner);
        }

        private readonly HashSet<TestEnemySpawner> _visited = new();

        public bool TryGetGuideTarget(out Vector3 position)
        {
            position = default;
            PlayerAgent player = Player;
            if (player == null)
                return false;

            Vector3 me = player.transform.position;
            float best = float.MaxValue;
            foreach (AbstractEnemy enemy in Object.FindObjectsByType<AbstractEnemy>(FindObjectsSortMode.None))
            {
                if (enemy == null || !enemy.IsAlive)
                    continue;
                float d = (enemy.transform.position - me).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    position = enemy.transform.position;
                }
            }
            if (best < float.MaxValue)
                return true;

            foreach (TestEnemySpawner spawner in Object.FindObjectsByType<TestEnemySpawner>(FindObjectsSortMode.None))
            {
                if (spawner.PlannedCount <= 0 || _visited.Contains(spawner))
                    continue;
                float d = (spawner.transform.position - me).sqrMagnitude;
                if (d < 7f * 7f)
                {
                    _visited.Add(spawner);
                    continue;
                }
                if (d < best)
                {
                    best = d;
                    position = spawner.transform.position;
                }
            }
            return best < float.MaxValue;
        }

        public string KeyLabel(SkillSlotId slot)
        {
            string raw = Hud != null ? Hud.KeyLabel(slot) : slot.ToString();
            return PrettyKey(raw);
        }

        public static string PrettyKey(string raw)
        {
            switch (raw)
            {
                case "RMB":
                case "Right Button":
                case "Right Click":
                    return "우클릭";
                case "LMB":
                case "Left Button":
                case "Left Click":
                    return "좌클릭";
                default:
                    return raw;
            }
        }

        public string Format(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('{') < 0)
                return text;

            text = text.Replace("{Fire}", Key("좌클릭"));
            text = text.Replace("{Confirm}", Key("Enter"));
            text = text.Replace("{Finisher}", Key(Hud != null && !string.IsNullOrEmpty(Hud.FinisherKey) ? PrettyKey(Hud.FinisherKey) : "R"));
            foreach (SkillSlotId slot in SkillSlots.All)
                text = text.Replace("{" + slot + "}", Key(KeyLabel(slot)));
            return System.Text.RegularExpressions.Regex.Replace(text, @"\{([^{}]+)\}", m => Key(m.Groups[1].Value));
        }

        public static string Key(string label) => $"<color=#D6B878>[{label}]</color>";

        internal void Dispose() => HoldStageSpawners(false);

        private static T FindInterface<T>() where T : class
        {
            foreach (MonoBehaviour behaviour in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (behaviour is T found)
                    return found;
            }
            return null;
        }
    }

    public sealed class TutorialInput
    {
        private bool _clicked;
        private bool _injConfirm, _injSpace, _injMove, _injFire;

        public void DebugPress(bool confirm = false, bool space = false, bool move = false, bool fire = false)
        {
            _injConfirm |= confirm;
            _injSpace |= space;
            _injMove |= move;
            _injFire |= fire;
        }

        public bool ConfirmPressed { get; private set; }
        public bool SpacePressed { get; private set; }
        public bool MovePressed { get; private set; }
        public bool FirePressed { get; private set; }

        internal void Click() => _clicked = true;

        internal void Tick()
        {
            Keyboard k = Keyboard.current;
            Mouse m = Mouse.current;

            ConfirmPressed = _clicked || (k != null && (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame));
            SpacePressed = k != null && k.spaceKey.wasPressedThisFrame;
            MovePressed = k != null && (k.wKey.wasPressedThisFrame || k.aKey.wasPressedThisFrame || k.sKey.wasPressedThisFrame || k.dKey.wasPressedThisFrame);
            FirePressed = m != null && m.leftButton.wasPressedThisFrame;
            ConfirmPressed |= _injConfirm;
            SpacePressed |= _injSpace;
            MovePressed |= _injMove;
            FirePressed |= _injFire;
            _clicked = _injConfirm = _injSpace = _injMove = _injFire = false;
        }

        public bool SkillPressed(TutorialContext ctx, SkillSlotId slot) =>
            ctx.Player != null && ctx.Player.Input.WasSkillPressed(slot);
    }

    internal sealed class TutorialStationary : MonoBehaviour
    {
        public AbstractEnemy Enemy;
        private Unity.Behavior.BehaviorGraphAgent _agent;
        private int _frames;
        private float _deadAt = -1f;

        private void Update()
        {
            if (Enemy == null)
            {
                Destroy(this);
                return;
            }

            if (Enemy.IsAlive)
            {
                if (_frames++ < 1)
                    return;
                if (_agent == null)
                    _agent = Enemy.GetComponent<Unity.Behavior.BehaviorGraphAgent>();
                if (_agent != null && _agent.enabled)
                {
                    Enemy.StopBehavior();
                    _agent.enabled = false;
                }
                if (_frames % 15 == 2)
                    Enemy.SkillModule?.CancelAllSkills();
                return;
            }

            if (_deadAt < 0f)
                _deadAt = Time.unscaledTime;
            else if (Time.unscaledTime - _deadAt > 0.5f)
            {
                Char.Visual.Fx.Shockwave(Enemy.transform.position, new Color(0.84f, 0.72f, 0.47f, 1f), 2f);
                Destroy(Enemy.gameObject);
            }
        }
    }

    internal static class TutorialAnchors
    {
        public static bool TryFindHud(string id, out RectTransform rect)
        {
            rect = null;
            Component found = null;

            if (id == TutorialAnchorIds.HudSkillBar)
                found = Object.FindFirstObjectByType<SkillBarView>();
            else if (id == TutorialAnchorIds.HudCombo)
                found = Object.FindFirstObjectByType<ComboUI>();
            else if (id == TutorialAnchorIds.HudQuest)
                found = Object.FindFirstObjectByType<KillQuestHud>();
            else if (id == TutorialAnchorIds.HudHp)
                found = Object.FindFirstObjectByType<PlayerHpHud>();
            else if (id.StartsWith(TutorialAnchorIds.HudSlotPrefix)
                     && System.Enum.TryParse(id.Substring(TutorialAnchorIds.HudSlotPrefix.Length), out SkillSlotId slot))
            {
                SkillBarView bar = Object.FindFirstObjectByType<SkillBarView>();
                if (bar != null && bar.TryGetView(slot, out SkillSlotView view))
                    found = view;
            }

            rect = found != null ? found.transform as RectTransform : null;
            return rect != null;
        }
    }
}
