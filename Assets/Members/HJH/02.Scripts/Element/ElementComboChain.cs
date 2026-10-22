using System;
using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using Assets.Members.HJH._02.Scripts.Char.Visual;
using Members.JJH._02_Scripts.ElementsSystem;
using Members.JJH._02_Scripts.Systems.EventChannelSystem;
using Members.JJH._02_Scripts.Systems.EventChannelSystem.Events;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Assets.Members.HJH._02.Scripts.Element
{
    // 스킬 콤보 체인 + 피니시 (콤보 테스트 씬에서 JJH ElementStackManager 대신 씀. 둘 중 하나만)
    //  - 원소 있는 스킬을 쓸 때마다 입력 하나. 다음 스킬을 chainWindow 안에 쓰면 체인이 이어짐
    //  - 마지막 입력들이 JJH 콤보 표의 콤보가 되면 바로 완성 (기다림 없음). 입력은 안 지워져서 콤보끼리 이어짐
    //  - 완성된 콤보(입력 2개 이상, 서로 다른 것만)는 피니시 스택 +1 (최대 maxStock)
    //  - 체인 끝이 피니시 스킬 콤보(FinisherBook, 원소 3개)면 그 피니시 스킬도 저장 (최대 maxStored, 스택도 +1)
    //  - C: 스택 전부 써서 readyTime 동안 "피니시 준비". 그 안에 쓰는 Q/E/우클릭/R 하나가
    //       피해 ×stackScale[스택] 로 강화되고, 저장된 피니시 스킬이 같이 터짐. 시간 안에 안 쓰면 날아감
    // 완성된 콤보는 JJH 쪽에 ElementBuffTriggeredEvent(콤보 입력들)로 보내서 ElementBuffController 가 버프를 검.
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
        [SerializeField, Tooltip("완성된 콤보를 JJH 버프로 보냄. 같은 씬에 JJH ElementStackManager 가 켜져 있으면 그쪽이 보내니까 끔 (안 끄면 버프 두 번)")]
        private bool sendBuffEvents = true;

        [Header("피니시 (C)")]
        [SerializeField] private Key finisherKey = Key.C;
        [SerializeField, Tooltip("피니시 스킬 콤보 표. 비우면 Resources/HJH/FinisherBook")] private FinisherBook finisherBook;
        [SerializeField, Min(1)] private int maxStored = 3;
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
        private readonly List<FinisherBook.Entry> _stored = new();
        private readonly List<FinisherBook.Entry> _armedSkills = new();
        private float _remaining;
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

        public string FinisherKeyLabel => finisherKey.ToString();
        public FinisherBook Finishers => finisherBook;
        public IReadOnlyList<FinisherBook.Entry> StoredFinishers => _stored;
        public bool IsArmed => _armedLeft > 0f;
        public float ArmedRemaining => Mathf.Max(0f, _armedLeft);
        public float ReadyTime => readyTime;
        public int ArmedStacks => _armedStacks;
        public float ArmedScale => ScaleFor(_armedStacks);
        public IReadOnlyList<FinisherBook.Entry> ArmedFinishers => _armedSkills;

        public float ScaleFor(int stacks) =>
            stacks <= 0 || stackScale == null || stackScale.Length == 0 ? 1f : stackScale[Mathf.Clamp(stacks - 1, 0, stackScale.Length - 1)];

        // 피니시를 실어 보낼 수 있는 칸 (대쉬·평타 제외)
        public static bool CanCarryFinisher(SkillSlotId slot) =>
            slot is SkillSlotId.Basic1 or SkillSlotId.Basic2 or SkillSlotId.Weapon or SkillSlotId.Ultimate;

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
        // 피니시 스킬 콤보 완성 → 저장됨
        public event Action<FinisherBook.Entry> FinisherStored;
        // C 눌러서 준비됨: (쓴 스택, 피해 배율)
        public event Action<int, float> FinisherArmed;
        // 준비 시간 안에 스킬을 안 씀
        public event Action FinisherExpired;
        // 강화된 스킬 나감: (칸, 피해 배율, 같이 터진 피니시 스킬들)
        public event Action<SkillSlotId, float, IReadOnlyList<FinisherBook.Entry>> FinisherReleased;

        private void Awake()
        {
            Book = ElementComboBook.From(buffController);
            if (finisherBook == null)
                finisherBook = FinisherBook.Default;
            maxInputs = Mathf.Max(maxInputs, Book.LongestLength);
            Debug.Assert(skills != null, $"{name}: ElementComboChain needs the player's SkillStateModule.");
            Debug.Assert(systemChannel != null, $"{name}: ElementComboChain needs the System event channel.");
        }

        private void OnEnable()
        {
            if (skills == null)
                return;
            skills.SkillUsed += HandleSkillUsed;
            skills.SkillCasting += HandleSkillCasting;
        }

        private void OnDisable()
        {
            if (skills == null)
                return;
            skills.SkillUsed -= HandleSkillUsed;
            skills.SkillCasting -= HandleSkillCasting;
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

        // 스킬 코드가 돌기 전에 강화를 걸어둠 → 바로 맞는 스킬도 배율이 들어감
        private void HandleSkillCasting(SkillUsedInfo info)
        {
            if (!IsArmed || info.Data == null || !CanCarryFinisher(info.Slot))
                return;

            skills.Context?.BoostSkillDamage(info.Data, ArmedScale, empowerTime);
            _armedLeft = 0f;
            _releasing = true;
        }

        // Skills without an element are not part of the combo language: they neither add nor break the chain.
        private void HandleSkillUsed(SkillUsedInfo info)
        {
            if (info.Data != null && info.Data.TryGetElement(out ElementType element))
                AddInput(new ComboInput(element, info.Slot));

            // 강화된 스킬이 먼저 나가고 피니시 스킬이 바로 뒤따라 터짐
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
                    systemChannel.RaiseEvent(SystemEvents.ElementBuffTriggeredEvent.Init(combo.Sequence));
                if (combo.Length > 1)   // 원소 하나짜리 콤보는 스킬마다 터져서 스택은 안 줌
                    AddToStock(combo);
            }

            if (finisherBook != null && finisherBook.TryFindAtEnd(_elements, out FinisherBook.Entry finisher))
                StoreFinisher(finisher);
        }

        private void AddToStock(ElementComboBook.Recipe combo)
        {
            _stock.RemoveAll(stocked => stocked.Name == combo.Name);
            if (_stock.Count >= maxStock)
                _stock.RemoveAt(0);
            _stock.Add(combo);
            StockChanged?.Invoke();
        }

        private void StoreFinisher(FinisherBook.Entry finisher)
        {
            _stored.RemoveAll(stored => stored.name == finisher.name);
            if (_stored.Count >= maxStored)
                _stored.RemoveAt(0);
            _stored.Add(finisher);

            var recipe = new ElementComboBook.Recipe(finisher.name, finisher.sequence);
            Debug.Log($"[ComboChain] 피니시 스킬 저장: {finisher.name}");
            HitFeel.Play(comboHitStop, comboShake);
            ComboLanded?.Invoke(new LandedCombo(recipe, _inputs.Count - recipe.Length));
            FinisherStored?.Invoke(finisher);
            AddToStock(recipe);
        }

        // C: 스택 전부 소모 → 준비. 스택 없거나 이미 준비 중이면 아무 일 없음
        private void TryArm()
        {
            if (IsArmed || _stock.Count == 0)
                return;

            _armedStacks = _stock.Count;
            _armedSkills.Clear();
            _armedSkills.AddRange(_stored);
            _armedLeft = readyTime;
            _stock.Clear();
            _stored.Clear();

            Debug.Log($"[ComboChain] 피니시 준비 x{_armedStacks} (피해 x{ArmedScale:0.0#}, 피니시 스킬 {_armedSkills.Count}개)");
            HitFeel.Play(comboHitStop, comboShake);
            FinisherArmed?.Invoke(_armedStacks, ArmedScale);
            StockChanged?.Invoke();
        }

        private void Expire()
        {
            _armedLeft = 0f;
            _armedStacks = 0;
            _armedSkills.Clear();
            Debug.Log("[ComboChain] 피니시 시간 끝 (못 씀)");
            FinisherExpired?.Invoke();
            StockChanged?.Invoke();
        }

        private void Release(SkillSlotId slot)
        {
            float scale = ArmedScale;
            ISkillContext context = skills.Context;
            var fired = new List<FinisherBook.Entry>(_armedSkills);

            foreach (FinisherBook.Entry finisher in fired)
            {
                if (finisher.skill == null || context == null)
                    continue;
                context.BoostSkillDamage(finisher.skill, scale, empowerTime);
                ISkillExecution execution = finisher.skill.Begin(context);
                if (execution != null)
                    context.Run(new FinisherRun(execution, Mathf.Max(0.1f, finisher.skill.Duration)));
            }

            Debug.Log($"[ComboChain] 피니시! x{_armedStacks} 피해 x{scale:0.0#} / 피니시 스킬 {fired.Count}개");
            HitFeel.Play(finisherHitStop, finisherShake);
            FinisherReleased?.Invoke(slot, scale, fired);

            _armedStacks = 0;
            _armedSkills.Clear();
            StockChanged?.Invoke();
        }

        private void Break()
        {
            if (sendBuffEvents && buffLeftoverOnBreak && _elements.Count > 0)
                systemChannel.RaiseEvent(SystemEvents.ElementBuffTriggeredEvent.Init(_elements.ToArray()));

            int finalCount = ChainCount;
            ChainBroken?.Invoke(finalCount);
            _inputs.Clear();
            _elements.Clear();
            ChainCount = 0;
            _remaining = 0f;
        }

        // 피니시 스킬이 여러 프레임짜리면 스킬 상태 없이 따로 돌려줌
        private sealed class FinisherRun : ISkillTimedEffect
        {
            private readonly ISkillExecution _execution;
            private readonly float _duration;
            private float _elapsed;

            public FinisherRun(ISkillExecution execution, float duration)
            {
                _execution = execution;
                _duration = duration;
            }

            public bool Tick(float deltaTime)
            {
                _elapsed += deltaTime;
                _execution.Tick(_elapsed);
                if (_elapsed < _duration)
                    return true;
                _execution.End();
                return false;
            }
        }
    }
}
