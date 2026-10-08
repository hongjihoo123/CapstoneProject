using System;
using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.Char.Character;
using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using Assets.Members.HJH._02.Scripts.Element;
using Assets.Members.HJH._02.Scripts.SkillSwap;
using Members.JJH._02_Scripts.ElementsSystem;
using Members.KYR._01_Scripts;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Hud
{
    // The one place HUD code reads from. UI scripts should use only this (and the data types it hands out:
    // SkillData, PassiveData, CharacterData, ElementComboBook.Recipe, SkillPickup, ElementType),
    // never SkillStateModule / ElementComboChain / SkillSwapInteractor directly. Then gameplay code can
    // change freely as long as this class keeps its shape, and UI can be rebuilt without touching gameplay.
    //
    // No wiring needed: PlayerHudSource.Instance finds (or adds) it on the player, and every reference
    // below is found automatically. Features missing from a scene just never raise their events.
    //
    //   Skills    GetSlot(slot) -> SlotInfo, Passive, Character, KeyLabel(slot)
    //             LoadoutChanged, SkillUsed, SkillDenied, SkillReady, CharacterChanged
    //   Combo     HasCombo, ComboInputs, ComboList, ComboStock, MaxComboStock, ComboTimeLeft, ComboWindow,
    //             FinisherKey, TryGetNextCombo(...), KeyFor(element)
    //             ComboChanged (redraw the trail), ComboLanded, ComboBroken, FinisherReleased
    //   Swap      SwapPickup (null = closed), InteractKey, SwapChanged, Swapped
    //   Elements  Palette, ColorOf(element / skill), ElementName(element)
    [DisallowMultipleComponent]
    public class PlayerHudSource : MonoBehaviour
    {
        // Everything a skill icon needs, already resolved (swap lock, charges, key, element color).
        public readonly struct SlotInfo
        {
            public readonly SkillSlotId Slot;
            public readonly SkillData Skill;
            public readonly string Key;
            // Time until the skill can be cast again (0 = castable now) and the length it counts down from.
            public readonly float CooldownRemaining;
            public readonly float CooldownDuration;
            public readonly int Charges;
            public readonly int MaxCharges;
            public readonly Color Color;

            public SlotInfo(SkillSlotId slot, SkillData skill, string key, float remaining, float duration,
                int charges, int maxCharges, Color color)
            {
                Slot = slot;
                Skill = skill;
                Key = key;
                CooldownRemaining = remaining;
                CooldownDuration = duration;
                Charges = charges;
                MaxCharges = maxCharges;
                Color = color;
            }

            public bool IsEmpty => Skill == null;
            public bool IsReady => Skill != null && CooldownRemaining <= 0f;
            // 1 = just used, 0 = ready (for radial fills).
            public float CooldownFill => CooldownDuration > 0f ? Mathf.Clamp01(CooldownRemaining / CooldownDuration) : 0f;
        }

        [Tooltip("All optional: found automatically when left empty.")]
        [SerializeField] private PlayerAgent player;
        [SerializeField] private ElementComboChain comboChain;
        [SerializeField] private SkillSwapInteractor swapInteractor;
        [SerializeField] private CharacterSwitcher characterSwitcher;
        [SerializeField] private ElementPalette palette;

        private static PlayerHudSource _instance;

        private SkillStateModule _skills;
        private Dictionary<ElementType, string> _elementKeys = new();
        private bool _bound;

        public static PlayerHudSource Instance
        {
            get
            {
                if (_instance != null)
                    return _instance;

                PlayerAgent agent = FindFirstObjectByType<PlayerAgent>();
                if (agent == null)
                    return null;

                _instance = agent.GetComponent<PlayerHudSource>();
                if (_instance == null)
                    _instance = agent.gameObject.AddComponent<PlayerHudSource>();
                return _instance;
            }
        }

        // ---------------------------------------------------------------- events

        public event Action LoadoutChanged;
        public event Action<SkillSlotId, SkillData> SkillUsed;
        public event Action<SkillSlotId> SkillDenied;
        public event Action<SkillSlotId> SkillReady;
        public event Action<CharacterData> CharacterChanged;

        public event Action ComboChanged;
        public event Action<ElementComboBook.Recipe> ComboLanded;
        public event Action ComboBroken;
        public event Action<IReadOnlyList<ElementComboBook.Recipe>> FinisherReleased;

        public event Action<SkillPickup> SwapChanged;
        // slot, skill that went to the floor, skill that came in
        public event Action<SkillSlotId, SkillData, SkillData> Swapped;

        // ---------------------------------------------------------------- skills

        public PassiveData Passive => Skills != null ? Skills.Passive : null;
        public CharacterData Character => characterSwitcher != null ? characterSwitcher.Current : null;

        public string KeyLabel(SkillSlotId slot) => ComboKeyMap.Key(Player, slot);

        public SlotInfo GetSlot(SkillSlotId slot)
        {
            SkillStateModule skills = Skills;
            SkillData skill = skills != null ? skills.GetSkill(slot) : null;
            if (skill == null)
                return new SlotInfo(slot, null, KeyLabel(slot), 0f, 0f, 0, 0, NeutralColor);

            int charges = skills.GetCharges(slot);
            float cooldown = skills.GetCooldownRemaining(slot);
            float lockLeft = skills.GetLockRemaining(slot);

            // Castable while a charge is left, unless a swap lock is still running.
            float remaining = 0f;
            float duration = skills.GetCooldownDuration(slot);
            if (charges <= 0)
                remaining = cooldown;
            if (lockLeft > remaining)
            {
                remaining = lockLeft;
                duration = skills.GetLockDuration(slot);
            }

            return new SlotInfo(slot, skill, KeyLabel(slot), remaining, duration, charges, skills.GetMaxCharges(slot), ColorOf(skill));
        }

        // ---------------------------------------------------------------- combo

        public bool HasCombo => comboChain != null;
        public IReadOnlyList<ElementComboChain.ComboInput> ComboInputs => comboChain != null ? comboChain.Inputs : Array.Empty<ElementComboChain.ComboInput>();
        public IReadOnlyList<ElementComboBook.Recipe> ComboList => comboChain != null ? comboChain.Book.Recipes : Array.Empty<ElementComboBook.Recipe>();
        public IReadOnlyList<ElementComboBook.Recipe> ComboStock => comboChain != null ? comboChain.Stock : Array.Empty<ElementComboBook.Recipe>();
        public int MaxComboStock => comboChain != null ? comboChain.MaxStock : 0;
        public float ComboTimeLeft => comboChain != null ? comboChain.Remaining : 0f;
        public float ComboWindow => comboChain != null ? comboChain.ChainWindow : 0f;
        public string FinisherKey => comboChain != null ? KeyLabel(comboChain.FinisherSlot) : string.Empty;

        // The element that continues the best combo in progress (false when no combo is under way).
        public bool TryGetNextCombo(out ElementType next, out ElementComboBook.Recipe combo, out int progress)
        {
            if (comboChain != null)
                return comboChain.TryGetNext(out next, out combo, out progress);

            next = default;
            combo = default;
            progress = 0;
            return false;
        }

        // Key(s) that cast this element with the current loadout: "Q", "Q/E", or "-" when none does.
        public string KeyFor(ElementType element) => ComboKeyMap.KeyFor(_elementKeys, element);

        // ---------------------------------------------------------------- swap

        public SkillPickup SwapPickup => swapInteractor != null ? swapInteractor.Choosing : null;
        public string InteractKey => swapInteractor != null ? swapInteractor.InteractKeyLabel : string.Empty;

        // ---------------------------------------------------------------- elements

        public ElementPalette Palette => palette;
        public Color NeutralColor => palette != null ? palette.NeutralColor : Color.gray;
        public Color ColorOf(ElementType element) => palette != null ? palette.ColorOf(element) : Color.white;
        public Color ColorOf(SkillData skill) =>
            skill != null && skill.TryGetElement(out ElementType element) ? ColorOf(element) : NeutralColor;
        public string ElementName(ElementType element) => ElementComboBook.KoreanName(element);

        // ---------------------------------------------------------------- wiring

        private PlayerAgent Player
        {
            get
            {
                Bind();
                return player;
            }
        }

        private SkillStateModule Skills
        {
            get
            {
                Bind();
                return _skills;
            }
        }

        private void Awake()
        {
            if (_instance == null)
                _instance = this;
        }

        private void Start() => Bind();

        // Lazy so getters work even when another script's Start runs first. Subscribes once the player's
        // SkillFsm exists (it is set in PlayerAgent.Awake).
        private void Bind()
        {
            if (_bound)
                return;

            if (player == null)
                player = GetComponentInParent<PlayerAgent>() ?? FindFirstObjectByType<PlayerAgent>();
            if (player == null || player.SkillFsm == null)
                return;

            _skills = player.SkillFsm;
            if (comboChain == null)
                comboChain = FindFirstObjectByType<ElementComboChain>();
            if (swapInteractor == null)
                swapInteractor = player.GetComponentInChildren<SkillSwapInteractor>() ?? FindFirstObjectByType<SkillSwapInteractor>();
            if (characterSwitcher == null)
                characterSwitcher = FindFirstObjectByType<CharacterSwitcher>();
            if (palette == null)
                palette = ElementPalette.Default;

            _bound = true;
            _elementKeys = ComboKeyMap.Build(player);

            _skills.LoadoutChanged += HandleLoadoutChanged;
            _skills.SkillUsed += HandleSkillUsed;
            _skills.SkillDenied += HandleSkillDenied;
            _skills.SkillReady += HandleSkillReady;

            if (comboChain != null)
            {
                comboChain.InputAdded += HandleComboInput;
                comboChain.StockChanged += HandleComboChanged;
                comboChain.ComboLanded += HandleComboLanded;
                comboChain.ChainBroken += HandleComboBroken;
                comboChain.FinisherReleased += HandleFinisher;
            }

            if (swapInteractor != null)
            {
                swapInteractor.ChoosingChanged += HandleSwapChanged;
                swapInteractor.Swapped += HandleSwapped;
            }

            if (characterSwitcher != null)
                characterSwitcher.CharacterChanged += HandleCharacterChanged;
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
            if (!_bound)
                return;

            _skills.LoadoutChanged -= HandleLoadoutChanged;
            _skills.SkillUsed -= HandleSkillUsed;
            _skills.SkillDenied -= HandleSkillDenied;
            _skills.SkillReady -= HandleSkillReady;

            if (comboChain != null)
            {
                comboChain.InputAdded -= HandleComboInput;
                comboChain.StockChanged -= HandleComboChanged;
                comboChain.ComboLanded -= HandleComboLanded;
                comboChain.ChainBroken -= HandleComboBroken;
                comboChain.FinisherReleased -= HandleFinisher;
            }

            if (swapInteractor != null)
            {
                swapInteractor.ChoosingChanged -= HandleSwapChanged;
                swapInteractor.Swapped -= HandleSwapped;
            }

            if (characterSwitcher != null)
                characterSwitcher.CharacterChanged -= HandleCharacterChanged;
        }

        private void HandleLoadoutChanged()
        {
            _elementKeys = ComboKeyMap.Build(player);
            LoadoutChanged?.Invoke();
        }

        private void HandleSkillUsed(SkillUsedInfo info) => SkillUsed?.Invoke(info.Slot, info.Data);
        private void HandleSkillDenied(SkillSlotId slot) => SkillDenied?.Invoke(slot);
        private void HandleSkillReady(SkillSlotId slot) => SkillReady?.Invoke(slot);
        private void HandleCharacterChanged(CharacterData character) => CharacterChanged?.Invoke(character);

        private void HandleComboInput(bool shifted) => ComboChanged?.Invoke();
        private void HandleComboChanged() => ComboChanged?.Invoke();
        private void HandleComboLanded(ElementComboChain.LandedCombo landed) => ComboLanded?.Invoke(landed.Combo);

        private void HandleComboBroken(int finalCount)
        {
            ComboBroken?.Invoke();
            ComboChanged?.Invoke();
        }

        private void HandleFinisher(IReadOnlyList<ElementComboBook.Recipe> released)
        {
            FinisherReleased?.Invoke(released);
            ComboChanged?.Invoke();
        }

        private void HandleSwapChanged(SkillPickup pickup) => SwapChanged?.Invoke(pickup);
        private void HandleSwapped(SkillSlotId slot, SkillData outgoing, SkillData incoming) => Swapped?.Invoke(slot, outgoing, incoming);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _instance = null;
    }
}
