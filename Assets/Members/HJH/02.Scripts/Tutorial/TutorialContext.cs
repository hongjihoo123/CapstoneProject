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
    // 단계들이 게임을 들여다보는 창구. 단계는 이것만 보고 판단함 (게임 코드를 직접 찾지 않음).
    //  - 입력(시간이 멈춰도 동작), 플레이어/HUD/방 클리어, UI 자리 찾기, 연습용 적 소환, 키 이름
    //  - 횟수 기록: 스킬 사용, 기본 공격, 콤보 성공, 이동 거리 → 단계는 Begin 때 값을 기억해 두고 늘었는지 비교
    //    (이벤트를 단계마다 구독하면 건너뛸 때 해제가 꼬이므로 여기 한 곳에서만 구독)
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

        // 확인 도구(자동 진행)용: 기본 공격 한 번 친 것으로
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

        // 연습 단계 시작 때 쿨타임을 비워 줌 (궁극기 20초 같은 대기 없이 바로 해 보게). 지금 쓰는 중인 스킬은 안 건드림
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
                    skills.Equip(slot, skill);   // 다시 끼우면 쿨타임·충전이 처음 상태
            }
        }

        // 이 원소를 내는 스킬 칸 (없으면 false)
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

        // ---------------------------------------------------------------- 매 프레임 (TutorialDirector 가 부름)

        internal void Tick(bool paused)
        {
            _ = Hud;   // 플레이어가 늦게 생겨도 구독되게
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
                    // 순간이동(대시 블링크 등)은 이동 연습으로 안 셈
                    if (delta.magnitude < 1.5f)
                        MovedDistance += delta.magnitude;
                }

                // PlayerAgent.Update 가 먼저 돌아서(실행 순서) 이번 프레임 입력이 이미 채워져 있음
                if (player.Input.FirePressed)
                    BasicAttacks++;
            }

            _lastPlayerPos = pos;
            _hasLastPos = true;
        }

        // ---------------------------------------------------------------- UI 자리

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

        // ---------------------------------------------------------------- 적

        // 플레이어 앞(조준 방향) distance 만큼에 연습용 적 하나. 내비 메시 위로 맞추고, 방 클리어 수에는 안 셈.
        // stationary = 행동 트리를 꺼서 제자리에 서 있게 (때리지도 않음)
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

        // 화면 안, 길이 뚫린 곳: 카메라 기준 오른쪽 → 왼쪽 → 위 → 아래 순으로 내비 메시 위 + 플레이어에서 막힘 없이 갈 수 있는 자리.
        // (조준 방향으로 두면 HUD 밑이나 벽 뒤에 생길 수 있음)
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
                        continue;   // 가는 길이 막힘
                    direction = dir;
                    return hit.position;
                }
            }

            direction = right;
            Vector3 fallback = origin + right * distance;
            return NavMesh.SamplePosition(fallback, out NavMeshHit any, 4f, NavMesh.AllAreas) ? any.position : fallback;
        }

        // 튜토리얼 동안 맵의 적 스포너를 멈춰 둠 (연습 중 엉뚱한 적이 몰려오지 않게). 방 클리어 수에는 그대로 들어감
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

        // 씬에 처음부터 서 있는 적(예: 시작 지점 근처 엔트 보스)도 연습 동안 멈춤: 행동 트리를 끄고, 쓰던 스킬 취소, 길찾기 정지.
        // 적 Start(행동 트리 변수 설정)가 끝난 뒤에 불러야 함 (TutorialDirector 가 첫 단계 전에 부름). 연습용 적은 제외.
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

        // 길 안내: 살아 있는 적 중 가장 가까운 적, 없으면 아직 안 가 본 스포너 중 가장 가까운 곳
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
                    _visited.Add(spawner);   // 이 근처까지 왔으면 스포너가 터졌다고 봄
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

        // ---------------------------------------------------------------- 글자

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

        // {Weapon} 같은 자리표시를 지금 키 이름으로 (금색)
        public string Format(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('{') < 0)
                return text;

            text = text.Replace("{Fire}", Key("좌클릭"));
            text = text.Replace("{Confirm}", Key("Enter"));
            text = text.Replace("{Finisher}", Key(Hud != null && !string.IsNullOrEmpty(Hud.FinisherKey) ? PrettyKey(Hud.FinisherKey) : "R"));
            foreach (SkillSlotId slot in SkillSlots.All)
                text = text.Replace("{" + slot + "}", Key(KeyLabel(slot)));
            // 나머지 {글자} 는 그 글자 그대로 키 모양으로 ({W A S D} → [W A S D])
            return System.Text.RegularExpressions.Regex.Replace(text, @"\{([^{}]+)\}", m => Key(m.Groups[1].Value));
        }

        public static string Key(string label) => $"<color=#D6B878>[{label}]</color>";

        // ---------------------------------------------------------------- 정리

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

    // 시간이 멈춰도 동작하는 입력 (새 입력 시스템 장치를 직접 읽음)
    public sealed class TutorialInput
    {
        private bool _clicked;
        private bool _injConfirm, _injSpace, _injMove, _injFire;

        // 확인 도구(자동 진행)용: 다음 프레임에 이 키를 누른 것으로
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

        // 카드의 "계속" 버튼
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

        // 스킬 키 (플레이어 입력 설정을 그대로 따름 → 키를 바꿔도 맞음)
        public bool SkillPressed(TutorialContext ctx, SkillSlotId slot) =>
            ctx.Player != null && ctx.Player.Input.WasSkillPressed(slot);
    }

    // 연습용 적 (때리지도 움직이지도 않는 표적)
    //  살아 있는 동안: 행동 트리 컴포넌트를 꺼 두고 쓰던 스킬을 취소 (StopBehavior 만으로는 공격을 계속했음)
    //  체력이 0 이 되면: 행동 트리가 꺼져 있어 원래 죽음 연출(행동 트리 → 애니메이션 → Dead)이 안 도니 잠깐 뒤 직접 치움
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
                // 적 Start(행동 트리 변수 설정)가 끝난 다음 프레임부터 끔
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

    // HUD 부품 찾아 주기 (인게임 앵커)
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
