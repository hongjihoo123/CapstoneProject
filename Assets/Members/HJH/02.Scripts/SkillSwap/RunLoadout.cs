using System.Collections;
using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.Char.Character;
using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using Members.KYR._01.Scripts.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Assets.Members.HJH._02.Scripts.SkillSwap
{
    // 씬이 바뀌어도 들고 있던 스킬(Q/E)과 고른 캐릭터를 그대로 들고 감. 전투(런)가 끝날 때까지.
    //  - 플레이 중 Q/E 바꾸거나 캐릭터 바꾸면 계속 기억해 둠 (쿨타임도 같이 → 씬 넘기기로 쿨 초기화 안 됨)
    //  - 새 씬이 열리면 씬에 박혀 있는 시작 스킬 대신 기억한 걸로 다시 끼움
    //  - 끝: 플레이어가 죽으면 자동으로 비움. 다른 곳(보스 클리어, 처음 화면 등)에서 끝낼 땐 RunLoadout.Clear() 부르면 됨
    // 씬에 따로 놓을 거 없음 (게임 켜질 때 알아서 붙음)
    public static class RunLoadout
    {
        private sealed class SlotSave
        {
            public SkillData Skill;
            public SkillCooldownState Cooldown;
        }

        private static readonly Dictionary<SkillSlotId, SlotSave> Slots = new();
        private static int _characterIndex = -1;

        public static bool HasSave { get; private set; }

        // 런 끝 (죽음, 클리어, 메인으로 나가기 등). 다음 씬부터는 씬에 박힌 시작 스킬로 시작함
        public static void Clear()
        {
            Slots.Clear();
            _characterIndex = -1;
            HasSave = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            Clear();
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Attach();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Single)
                Attach();
        }

        private static void Attach()
        {
            PlayerAgent player = Object.FindAnyObjectByType<PlayerAgent>();
            if (player != null && !player.TryGetComponent(out RunLoadoutKeeper _))
                player.gameObject.AddComponent<RunLoadoutKeeper>();
        }

        internal static void Save(SkillStateModule skills, CharacterSwitcher switcher)
        {
            if (skills == null)
                return;

            foreach (SkillSlotId slot in SkillSlots.All)
            {
                if (!SkillSlots.IsSwappable(slot))
                    continue;
                Slots[slot] = new SlotSave { Skill = skills.GetSkill(slot), Cooldown = skills.GetCooldownState(slot) };
            }

            if (switcher != null && switcher.CurrentIndex >= 0)
                _characterIndex = switcher.CurrentIndex;
            HasSave = true;
        }

        internal static void Restore(SkillStateModule skills, CharacterSwitcher switcher)
        {
            if (!HasSave || skills == null)
                return;

            // 캐릭터 먼저 (캐릭터 바꾸면 Space/우클릭/R 이 그 캐릭터 걸로 바뀜), 그다음 Q/E
            if (switcher != null && _characterIndex >= 0)
                switcher.Select(_characterIndex);

            foreach (KeyValuePair<SkillSlotId, SlotSave> pair in Slots)
                skills.Equip(pair.Key, pair.Value.Skill, pair.Value.Cooldown, 0f);
        }
    }

    // 플레이어에 자동으로 붙는 부분: 시작할 때 복원, 그 뒤로 바뀔 때마다 저장, 죽으면 런 끝
    [DisallowMultipleComponent]
    public class RunLoadoutKeeper : MonoBehaviour
    {
        private PlayerAgent _player;
        private SkillStateModule _skills;
        private CharacterSwitcher _switcher;
        private bool _ready;
        private bool _ended;

        private IEnumerator Start()
        {
            _player = GetComponent<PlayerAgent>();
            // 씬의 시작 스킬·시작 캐릭터가 다 끼워진 다음에 덮어써야 해서 한 프레임 기다림
            yield return null;

            _skills = _player != null ? _player.SkillFsm : null;
            _switcher = FindAnyObjectByType<CharacterSwitcher>();
            if (_skills == null)
                yield break;

            RunLoadout.Restore(_skills, _switcher);
            RunLoadout.Save(_skills, _switcher);

            _skills.LoadoutChanged += SaveNow;
            if (_switcher != null)
                _switcher.CharacterChanged += OnCharacterChanged;
            _ready = true;
        }

        private void OnDestroy()
        {
            if (_skills != null)
                _skills.LoadoutChanged -= SaveNow;
            if (_switcher != null)
                _switcher.CharacterChanged -= OnCharacterChanged;
        }

        private void OnCharacterChanged(CharacterData _) => SaveNow();

        private void SaveNow()
        {
            if (_ready && !_ended)
                RunLoadout.Save(_skills, _switcher);
        }

        private void Update()
        {
            if (!_ready || _ended || _player == null || _player.IsAlive)
                return;

            _ended = true;
            RunLoadout.Clear();
        }

        // 씬 넘어가기 직전 쿨타임까지 최신으로
        private void OnDisable() => SaveNow();
    }
}
