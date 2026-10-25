using Members.JJH._02_Scripts.Systems.EventChannelSystem;
using Members.JJH._02_Scripts.Systems.EventChannelSystem.Events;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Members.JJH._02_Scripts.ElementsSystem
{
    public class ElementBuffController : MonoBehaviour
    {
        [Serializable]
        public class ElementCombo
        {
            public string name;
            public ElementType[] sequence;
            [Tooltip("효과 설명 (아직 실제 효과는 안 걸림, 글자만)")]
            public string effect;
            [Tooltip("지속 시간(초). 같은 콤보를 다시 터뜨리면 시간만 처음부터 다시")]
            public float duration;
        }

        [Header("Event")]
        [SerializeField] private EventChannelSO systemChannel;

        [Header("Combos")]
        [SerializeField]
        private List<ElementCombo> combos = new()
        {
            // 단일 속성 (해당 속성 스킬 쓸 때마다 터짐)
            new ElementCombo { name = "불", sequence = new[] { ElementType.Fire }, effect = "불 속성 피해 증가 3%", duration = 30f },
            new ElementCombo { name = "물", sequence = new[] { ElementType.Water }, effect = "물 속성 피해 증가 3%", duration = 30f },
            new ElementCombo { name = "바람", sequence = new[] { ElementType.Wind }, effect = "바람 속성 피해 증가 3%", duration = 30f },
            new ElementCombo { name = "땅", sequence = new[] { ElementType.Earth }, effect = "땅 속성 피해 증가 3%", duration = 30f },
            new ElementCombo { name = "번개", sequence = new[] { ElementType.Electric }, effect = "번개 속성 피해 증가 3%", duration = 30f },

            // 2개 조합 (마지막 입력들이 맞으면 터짐, 길이가 긴 콤보가 단일 콤보보다 우선)
            new ElementCombo { name = "불-번개", sequence = new[] { ElementType.Fire, ElementType.Electric }, effect = "공격 증가 10%", duration = 10f },
            new ElementCombo { name = "물-물", sequence = new[] { ElementType.Water, ElementType.Water }, effect = "체력 회복 8%", duration = 10f },
            new ElementCombo { name = "땅-물", sequence = new[] { ElementType.Earth, ElementType.Water }, effect = "쉴드 20", duration = 20f },
            new ElementCombo { name = "번개-번개", sequence = new[] { ElementType.Electric, ElementType.Electric }, effect = "공격 속도 증가 20%", duration = 15f },
        };

        // 지금 켜져 있는 콤보와 남은 시간 (효과는 아직 없고 시간만 관리)
        private readonly Dictionary<string, float> _active = new();

        private int[] _counts;

        public int ComboCount => combos.Count;

        private void Awake()
        {
            _counts = new int[Enum.GetValues(typeof(ElementType)).Length];
        }

        private void OnEnable()
        {
            systemChannel.AddListener<ElementStackChangedEvent>(HandleStackChanged);
            systemChannel.AddListener<ElementBuffTriggeredEvent>(HandleTriggered);
        }

        private void OnDisable()
        {
            systemChannel.RemoveListener<ElementStackChangedEvent>(HandleStackChanged);
            systemChannel.RemoveListener<ElementBuffTriggeredEvent>(HandleTriggered);
        }

        private void HandleStackChanged(ElementStackChangedEvent evt)
        {
            if (evt.Stacks == null || evt.Stacks.Count == 0)
                return;

            if (TryFindCombo(evt.Stacks, out ElementCombo combo))
            {
                systemChannel.RaiseEvent(
                    SystemEvents.ElementBuffTriggeredEvent.Init(evt.Stacks));

                return;
            }

            if (TryFindNextCombo(evt.Stacks, out ElementCombo nextCombo, out int progress))
            {
                ElementType nextElement = nextCombo.sequence[progress];

                systemChannel.RaiseEvent(SystemEvents.ElementComboNextEvent
                    .Init(nextElement, nextCombo.name, progress, nextCombo.sequence.Length));
            }
        }

        private void HandleTriggered(ElementBuffTriggeredEvent evt)
        {
            if (evt.Stacks == null || evt.Stacks.Count == 0)
                return;

            if (TryFindCombo(evt.Stacks, out ElementCombo combo))
            {
                ApplyCombo(combo);

                systemChannel.RaiseEvent(
                    SystemEvents.ElementComboCompleteEvent.Init(
                        combo.name,
                        combo.sequence));

                return;
            }

            Array.Clear(_counts, 0, _counts.Length);

            for (int i = 0; i < evt.Stacks.Count; i++)
                _counts[(int)evt.Stacks[i]]++;

            for (int i = 0; i < _counts.Length; i++)
            {
                if (_counts[i] <= 0)
                    continue;

                ApplyElement((ElementType)i, _counts[i]);
            }
        }

        private bool TryFindCombo(
            IReadOnlyList<ElementType> stacks,
            out ElementCombo combo)
        {
            combo = null;

            if (stacks == null || stacks.Count == 0)
                return false;

            int lastIndex = stacks.Count - 1;

            for (int i = 0; i < combos.Count; i++)
            {
                ElementCombo current = combos[i];

                if (current.sequence == null || current.sequence.Length == 0)
                    continue;

                if (current.sequence.Length > stacks.Count)
                    continue;

                int start = stacks.Count - current.sequence.Length;

                if (start < 0)
                    continue;

                bool matched = true;

                for (int j = 0; j < current.sequence.Length; j++)
                {
                    if (stacks[start + j] != current.sequence[j])
                    {
                        matched = false;
                        break;
                    }
                }

                if (!matched)
                    continue;

                if (start + current.sequence.Length - 1 != lastIndex)
                    continue;

                combo = current;
                return true;
            }

            return false;
        }

        private bool TryFindNextCombo(
            IReadOnlyList<ElementType> stacks,
            out ElementCombo combo,
            out int progress)
        {
            combo = null;
            progress = 0;

            if (stacks == null || stacks.Count == 0)
                return false;

            int lastIndex = stacks.Count - 1;

            for (int i = 0; i < combos.Count; i++)
            {
                ElementCombo current = combos[i];

                if (current.sequence == null || current.sequence.Length <= 1)
                    continue;

                if (current.sequence.Length > stacks.Count)
                {
                    if (stacks.Count > current.sequence.Length)
                        continue;

                    bool matched = true;

                    for (int j = 0; j < stacks.Count; j++)
                    {
                        if (stacks[j] != current.sequence[j])
                        {
                            matched = false;
                            break;
                        }
                    }

                    if (matched)
                    {
                        combo = current;
                        progress = stacks.Count;
                        return true;
                    }

                    continue;
                }

                int comboStart = lastIndex - current.sequence.Length + 1;

                if (comboStart < 0)
                    continue;

                bool fullMatch = true;

                for (int j = 0; j < current.sequence.Length; j++)
                {
                    if (stacks[comboStart + j] != current.sequence[j])
                    {
                        fullMatch = false;
                        break;
                    }
                }

                if (fullMatch)
                    continue;

                for (int start = 0; start <= lastIndex; start++)
                {
                    int matched = 0;

                    while (start + matched <= lastIndex &&
                           matched < current.sequence.Length &&
                           stacks[start + matched] == current.sequence[matched])
                    {
                        matched++;
                    }

                    if (matched == 0)
                        continue;

                    if (start + matched - 1 != lastIndex)
                        continue;

                    if (matched >= current.sequence.Length)
                        continue;

                    combo = current;
                    progress = matched;
                    return true;
                }
            }

            return false;
        }

        // 콤보 효과는 아직 안 만듦. 지속 시간만 관리함:
        // 처음 터지면 시간 시작, 이미 켜져 있는 콤보가 또 터지면 겹치지 않고 시간만 처음부터 다시
        private void ApplyCombo(ElementCombo combo)
        {
            bool refreshed = _active.ContainsKey(combo.name);
            _active[combo.name] = combo.duration;

            Debug.Log(refreshed
                ? $"[ElementBuff] Combo: {combo.name} ({combo.effect}) 시간 초기화 -> {combo.duration}초"
                : $"[ElementBuff] Combo: {combo.name} ({combo.effect}) 시작, {combo.duration}초");
        }

        private readonly List<string> _ended = new();
        private readonly List<string> _names = new();

        private void Update()
        {
            if (_active.Count == 0)
                return;

            _ended.Clear();
            _names.Clear();
            _names.AddRange(_active.Keys);   // 돌면서 값 바꿔야 해서 이름만 따로 복사
            foreach (string name in _names)
            {
                float left = _active[name] - Time.deltaTime;
                if (left > 0f)
                    _active[name] = left;
                else
                    _ended.Add(name);
            }

            foreach (string name in _ended)
            {
                _active.Remove(name);
                Debug.Log($"[ElementBuff] Combo: {name} 끝");
            }
        }

        // 남은 시간 (UI 에서 쓰려고). 꺼져 있으면 false
        public bool TryGetRemaining(string comboName, out float seconds) => _active.TryGetValue(comboName, out seconds);

        private void ApplyElement(ElementType type, int count)
        {
            Debug.Log($"[ElementBuff] {type} x{count}");
        }

        public string GetComboName(int index)
        {
            if (index < 0 || index >= combos.Count)
                return string.Empty;

            return combos[index].name;
        }

        public IReadOnlyList<ElementType> GetComboSequence(int index)
        {
            if (index < 0 || index >= combos.Count)
                return null;

            return combos[index].sequence;
        }
    }
}