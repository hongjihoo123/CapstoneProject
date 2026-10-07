using System;
using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using Members.JJH._02_Scripts.ElementsSystem;
using Members.JJH._02_Scripts.Systems.EventChannelSystem;
using Members.JJH._02_Scripts.Systems.EventChannelSystem.Events;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Element
{
    // Skill combo chain (replaces JJH's ElementStackManager in the combo test scene; use one or the other).
    //  - every element skill cast is a chain input; the chain lives while the next cast comes within chainWindow,
    //  - the moment the last inputs spell a combo from JJH's table, the combo lands (immediately, no waiting),
    //  - inputs are not consumed, so combos can overlap and link into each other,
    //  - every landed combo is also stocked (unique, up to maxStock); casting finisherSlot releases the
    //    whole stock at once, empowered (finisher), after its own input has been counted.
    // Landed combos are sent to JJH as ElementBuffTriggeredEvent(combo inputs), so ElementBuffController
    // resolves them exactly like before. Effects are still Debug.Log there.
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
        [SerializeField] private EventChannelSO systemChannel;
        [SerializeField, Tooltip("Combo table source (its combos list).")]
        private ElementBuffController buffController;
        [SerializeField, Min(1), Tooltip("Inputs kept for matching and display. At least the longest combo.")]
        private int maxInputs = 4;
        [SerializeField, Min(0.1f), Tooltip("Seconds allowed between casts before the chain breaks.")]
        private float chainWindow = 3f;
        [SerializeField, Min(1)] private int maxStock = 3;
        [SerializeField] private SkillSlotId finisherSlot = SkillSlotId.Ultimate;
        [SerializeField, Tooltip("On chain break, send the leftover inputs to JJH as plain per-element buffs (JJH's old fallback).")]
        private bool buffLeftoverOnBreak;

        private readonly List<ComboInput> _inputs = new();
        private readonly List<ElementType> _elements = new();
        private readonly List<ElementComboBook.Recipe> _stock = new();
        private float _remaining;

        public ElementComboBook Book { get; private set; } = new();
        public IReadOnlyList<ComboInput> Inputs => _inputs;
        public IReadOnlyList<ElementType> Elements => _elements;
        public int MaxInputs => maxInputs;
        public int ChainCount { get; private set; }
        public float ChainWindow => chainWindow;
        public float Remaining => _remaining;
        public IReadOnlyList<ElementComboBook.Recipe> Stock => _stock;
        public int MaxStock => maxStock;
        public SkillSlotId FinisherSlot => finisherSlot;
        public SkillStateModule Skills => skills;

        // The combo the chain is furthest into and the element that advances it (what the UI calls NEXT).
        public bool TryGetNext(out ElementType next, out ElementComboBook.Recipe route, out int progress)
        {
            bool onRoute = Book.TryFindBestProgress(_elements, out route, out progress);
            next = onRoute ? route.Sequence[progress] : default;
            return onRoute;
        }

        // shifted = the input list was full and the oldest input dropped out.
        public event Action<bool> InputAdded;
        public event Action<LandedCombo> ComboLanded;
        // Final chain count, raised before the inputs are cleared.
        public event Action<int> ChainBroken;
        public event Action StockChanged;
        // Released combos, raised before the stock is emptied.
        public event Action<IReadOnlyList<ElementComboBook.Recipe>> FinisherReleased;

        private void Awake()
        {
            Book = ElementComboBook.From(buffController);
            maxInputs = Mathf.Max(maxInputs, Book.LongestLength);
            Debug.Assert(skills != null, $"{name}: ElementComboChain needs the player's SkillStateModule.");
            Debug.Assert(systemChannel != null, $"{name}: ElementComboChain needs the System event channel.");
        }

        private void OnEnable()
        {
            if (skills != null)
                skills.SkillUsed += HandleSkillUsed;
        }

        private void OnDisable()
        {
            if (skills != null)
                skills.SkillUsed -= HandleSkillUsed;
        }

        private void Update()
        {
            if (ChainCount == 0)
                return;

            _remaining -= Time.deltaTime;
            if (_remaining <= 0f)
                Break();
        }

        // Skills without an element are not part of the combo language: they neither add nor break the chain.
        private void HandleSkillUsed(SkillUsedInfo info)
        {
            if (info.Data != null && info.Data.TryGetElement(out ElementType element))
                AddInput(new ComboInput(element, info.Slot));

            if (info.Slot == finisherSlot)
                ReleaseFinisher();
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

            if (!Book.TryFindComboAtEnd(_elements, out ElementComboBook.Recipe combo))
                return;

            Debug.Log($"[ComboChain] 콤보: {combo.Name} ({string.Join(" → ", combo.Sequence)}) / 체인 {ChainCount}");
            ComboLanded?.Invoke(new LandedCombo(combo, _inputs.Count - combo.Length));
            systemChannel.RaiseEvent(SystemEvents.ElementBuffTriggeredEvent.Init(combo.Sequence));
            AddToStock(combo);
        }

        private void AddToStock(ElementComboBook.Recipe combo)
        {
            _stock.RemoveAll(stocked => stocked.Name == combo.Name);
            if (_stock.Count >= maxStock)
                _stock.RemoveAt(0);
            _stock.Add(combo);
            StockChanged?.Invoke();
        }

        private void ReleaseFinisher()
        {
            if (_stock.Count == 0)
                return;

            var released = new List<ElementComboBook.Recipe>(_stock);
            var names = new List<string>();
            foreach (ElementComboBook.Recipe combo in released)
                names.Add(combo.Name);
            Debug.Log($"[ComboChain] 피니시 x{released.Count} (강화): {string.Join(", ", names)}");

            FinisherReleased?.Invoke(released);
            foreach (ElementComboBook.Recipe combo in released)
                systemChannel.RaiseEvent(SystemEvents.ElementBuffTriggeredEvent.Init(combo.Sequence));

            _stock.Clear();
            StockChanged?.Invoke();
        }

        private void Break()
        {
            if (buffLeftoverOnBreak && _elements.Count > 0)
                systemChannel.RaiseEvent(SystemEvents.ElementBuffTriggeredEvent.Init(_elements.ToArray()));

            int finalCount = ChainCount;
            ChainBroken?.Invoke(finalCount);
            _inputs.Clear();
            _elements.Clear();
            ChainCount = 0;
            _remaining = 0f;
        }
    }
}
