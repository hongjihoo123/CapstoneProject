using System.Collections;
using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.Char.Character;
using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using Members.KYR._01.Scripts.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Assets.Members.HJH._02.Scripts.SkillSwap
{
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

            if (switcher != null && _characterIndex >= 0)
                switcher.Select(_characterIndex);

            foreach (KeyValuePair<SkillSlotId, SlotSave> pair in Slots)
                skills.Equip(pair.Key, pair.Value.Skill, pair.Value.Cooldown, 0f);
        }
    }

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

        private void OnDisable() => SaveNow();
    }
}
