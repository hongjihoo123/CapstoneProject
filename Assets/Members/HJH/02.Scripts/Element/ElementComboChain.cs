using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using Assets.Members.HJH._02.Scripts.Char.Visual;
using Assets.Members.HJH._02.Scripts.Element.Upgrade;
using Members.JJH._02_Scripts.ElementsSystem;
using Members.JJH._02_Scripts.Systems.EventChannelSystem;
using Members.JJH._02_Scripts.Systems.EventChannelSystem.Events;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Assets.Members.HJH._02.Scripts.Element
{
    public class ElementComboChain : MonoBehaviour
    {
        public readonly struct ComboInput
        {
            public readonly ElementType Element;
            public readonly SkillSlotId Slot;

            public ComboInput(ElementType element, SkillSlotId slot)
            {
                Element = element;
                Slot = slot;
            }
        }

        public readonly struct LandedCombo
        {
            public readonly ElementComboBook.Recipe Combo;
            public readonly int StartIndex;

            public LandedCombo(ElementComboBook.Recipe combo, int startIndex)
            {
                Combo = combo;
                StartIndex = startIndex;
            }
        }

        [SerializeField] private SkillStateModule skills;
        [SerializeField] private EventChannelSO elementsChannel;
        [SerializeField, Tooltip("Combo table source (its combos list).")]
        private ElementBuffController buffController;
        [SerializeField, Min(1), Tooltip("Inputs kept for matching and display. At least the longest combo.")]
        private int maxInputs = 4;
        [SerializeField, Min(0.1f), Tooltip("Seconds allowed between casts before the chain breaks.")]
        private float chainWindow = 5f;
        [SerializeField, Min(1)] private int maxStock = 3;
        [SerializeField, Tooltip("완성된 콤보를 JJH 버프로 보냄. 같은 씬에 JJH ElementStackManager 가 켜져 있으면 그쪽이 보내니까 끔 (안 끄면 버프 두 번)")]
        private bool sendBuffEvents = true;

        [Header("피니시 (C)")]
        [SerializeField] private Key finisherKey = Key.C;
        [SerializeField, Tooltip("C 누르고 스킬 쓸 수 있는 시간")] private float readyTime = 5f;
        [SerializeField, Tooltip("쓴 스택 수별 피해 배율 (0번 = 1스택)")] private float[] stackScale = { 1.3f, 1.6f, 2f };
        [SerializeField, Tooltip("강화된 스킬이 배율을 유지하는 시간 (늦게 맞는 투사체, 지연 폭발도 포함되게)")] private float empowerTime = 4f;

        [Header("Feel")]
        [SerializeField, Tooltip("Hit stop + camera shake when a combo lands / the finisher fires (game feel, not UI).")]
        private float comboHitStop = 0.03f;
        [SerializeField] private float comboShake = 0.12f;
        [SerializeField] private float finisherHitStop = 0.08f;
        [SerializeField] private float finisherShake = 0.35f;
        [SerializeField, Tooltip("On chain break, send the leftover inputs to JJH as plain per-element buffs (JJH's old fallback).")]
        private bool buffLeftoverOnBreak;

        private readonly List<ComboInput> _inputs = new();
        private readonly List<ElementType> _elements = new();
        private readonly List<ElementComboBook.Recipe> _stock = new();
        private float _remaining;
        private int _baseInputs;
        private float _armedLeft;
        private int _armedStacks;
        private bool _releasing;

        public ElementComboBook Book { get; private set; } = new();
        public IReadOnlyList<ComboInput> Inputs => _inputs;
        public IReadOnlyList<ElementType> Elements => _elements;
        public int MaxInputs => maxInputs;
        public int ChainCount { get; private set; }
        public float ChainWindow => chainWindow;
        public float Remaining => _remaining;
        public IReadOnlyList<ElementComboBook.Recipe> Stock => _stock;
        public int MaxStock => maxStock;
        public SkillStateModule Skills => skills;

        public bool IsNextElement(ElementType element)
        {
            if (_elements.Count == 0)
                return false;

            foreach (ElementComboBook.Recipe recipe in Book.Recipes)
            {
                int progress = recipe.TailProgress(_elements);
                if (progress > 0 && progress < recipe.Length && recipe.Sequence[progress] == element)
                    return true;
            }
            return false;
        }

        public bool TryGetEffectRemaining(string comboName, out float seconds)
        {
            seconds = 0f;
            return buffController != null && buffController.TryGetRemaining(comboName, out seconds);
        }

        public string FinisherKeyLabel => finisherKey.ToString();
        public bool IsArmed => _armedLeft > 0f;
        public float ArmedRemaining => Mathf.Max(0f, _armedLeft);
        public float ReadyTime => readyTime;
        public int ArmedStacks => _armedStacks;
        public float ArmedScale => ScaleFor(_armedStacks);

        public float ScaleFor(int stacks) =>
            stacks <= 0 || stackScale == null || stackScale.Length == 0
                ? 1f
                : 1f + (stackScale[Mathf.Clamp(stacks - 1, 0, stackScale.Length - 1)] - 1f) * SkillUpgrades.Multiplier(SkillUpgradeKind.FinisherPower);

        public static bool CanCarryFinisher(SkillSlotId slot) =>
            slot is SkillSlotId.Basic1 or SkillSlotId.Basic2 or SkillSlotId.Weapon or SkillSlotId.Ultimate;

        public bool TryGetNext(out ElementType next, out ElementComboBook.Recipe route, out int progress)
        {
            bool onRoute = Book.TryFindBestProgress(_elements, out route, out progress);
            next = onRoute ? route.Sequence[progress] : default;
            return onRoute;
        }

        public event Action<bool> InputAdded;
        public event Action<LandedCombo> ComboLanded;
        public event Action<int> ChainBroken;
        public event Action StockChanged;
        public event Action BookChanged;
        public event Action<int, float> FinisherArmed;
        public event Action FinisherExpired;
        public event Action<SkillSlotId, float> FinisherReleased;

        private void Awake()
        {
            ElementStackManager stackManager = FindFirstObjectByType<ElementStackManager>();
            if (stackManager != null)
                chainWindow = stackManager.TriggerDelay;

            _baseInputs = maxInputs;
            RebuildBook();
            Debug.Assert(skills != null, $"{name}: ElementComboChain needs the player's SkillStateModule.");
            Debug.Assert(elementsChannel != null, $"{name}: ElementComboChain needs the System event channel.");
        }

        private void Start()
        {
            EnsureStarter();
            RefreshStackViews();
        }

        private void OnEnable()
        {
            ComboLoadout.Changed += HandleLoadoutChanged;
            if (skills == null)
                return;
            skills.SkillUsed += HandleSkillUsed;
            skills.SkillCasting += HandleSkillCasting;
            skills.LoadoutChanged += EnsureStarter;
        }

        private void OnDisable()
        {
            ComboLoadout.Changed -= HandleLoadoutChanged;
            if (skills == null)
                return;
            skills.SkillUsed -= HandleSkillUsed;
            skills.SkillCasting -= HandleSkillCasting;
            skills.LoadoutChanged -= EnsureStarter;
        }

        private void EnsureStarter()
        {
            if (skills != null)
                ComboLoadout.EnsureStarterFor(skills.GetSkill(SkillSlotId.Weapon), skills.GetSkill(SkillSlotId.Ultimate));
        }

        private void HandleLoadoutChanged()
        {
            RebuildBook();
            if (ChainCount > 0)
                Break();
            RefreshStackViews();
            BookChanged?.Invoke();
        }

        private void RebuildBook()
        {
            ComboLoadout.ApplyTo(buffController);
            Book = ElementComboBook.From(buffController);
            maxInputs = Mathf.Max(_baseInputs, Book.LongestLength);
        }

        private static void RefreshStackViews()
        {
            foreach (ElementStackView view in FindObjectsByType<ElementStackView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                view.RebuildComboList(true, ComboLoadout.EffectOf);
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard[finisherKey].wasPressedThisFrame)
                TryArm();

            if (_armedLeft > 0f)
            {
                _armedLeft -= Time.deltaTime;
                if (_armedLeft <= 0f)
                    Expire();
            }

            if (ChainCount == 0)
                return;

            _remaining -= Time.deltaTime;
            if (_remaining <= 0f)
                Break();
        }

        private void HandleSkillCasting(SkillUsedInfo info)
        {
            if (!IsArmed || info.Data == null || !CanCarryFinisher(info.Slot))
                return;

            skills.Context?.BoostSkillDamage(info.Data, ArmedScale, empowerTime);
            _armedLeft = 0f;
            _releasing = true;
        }

        private void HandleSkillUsed(SkillUsedInfo info)
        {
            if (info.Data != null && info.Data.TryGetElement(out ElementType element))
                AddInput(new ComboInput(element, info.Slot));

            if (_releasing)
            {
                _releasing = false;
                Release(info.Slot);
            }
        }

        private void AddInput(ComboInput input)
        {
            bool shifted = _inputs.Count >= maxInputs;
            if (shifted)
            {
                _inputs.RemoveAt(0);
                _elements.RemoveAt(0);
            }

            _inputs.Add(input);
            _elements.Add(input.Element);
            ChainCount++;
            _remaining = chainWindow;
            InputAdded?.Invoke(shifted);

            if (Book.TryFindComboAtEnd(_elements, out ElementComboBook.Recipe combo))
            {
                Debug.Log($"[ComboChain] 콤보: {combo.Name} ({string.Join(" → ", combo.Sequence)}) / 체인 {ChainCount}");
                HitFeel.Play(comboHitStop, comboShake);
                ComboLanded?.Invoke(new LandedCombo(combo, _inputs.Count - combo.Length));
                if (sendBuffEvents)
                    elementsChannel.RaiseEvent(ElementEvents.ElementBuffTriggeredEvent.Init(combo.Sequence));
                if (combo.Length > 1)
                    AddToStock(combo);
            }
        }

        private void AddToStock(ElementComboBook.Recipe combo)
        {
            _stock.RemoveAll(stocked => stocked.Name == combo.Name);
            if (_stock.Count >= maxStock)
                _stock.RemoveAt(0);
            _stock.Add(combo);
            StockChanged?.Invoke();
        }

        private void TryArm()
        {
            if (IsArmed || _stock.Count == 0)
                return;

            _armedStacks = _stock.Count;
            _armedLeft = readyTime;
            _stock.Clear();

            Debug.Log($"[ComboChain] 피니시 준비 x{_armedStacks} (피해 x{ArmedScale:0.0#})");
            HitFeel.Play(comboHitStop, comboShake);
            FinisherArmed?.Invoke(_armedStacks, ArmedScale);
            StockChanged?.Invoke();
        }

        private void Expire()
        {
            _armedLeft = 0f;
            _armedStacks = 0;
            Debug.Log("[ComboChain] 피니시 시간 끝 (못 씀)");
            FinisherExpired?.Invoke();
            StockChanged?.Invoke();
        }

        private void Release(SkillSlotId slot)
        {
            float scale = ArmedScale;
            Debug.Log($"[ComboChain] 피니시! x{_armedStacks} 피해 x{scale:0.0#}");
            HitFeel.Play(finisherHitStop, finisherShake);
            FinisherReleased?.Invoke(slot, scale);

            _armedStacks = 0;
            StockChanged?.Invoke();
        }

        private void Break()
        {
            if (sendBuffEvents && buffLeftoverOnBreak && _elements.Count > 0)
                elementsChannel.RaiseEvent(ElementEvents.ElementBuffTriggeredEvent.Init(_elements.ToArray()));

            int finalCount = ChainCount;
            ChainBroken?.Invoke(finalCount);
            _inputs.Clear();
            _elements.Clear();
            ChainCount = 0;
            _remaining = 0f;
        }
    }
}
