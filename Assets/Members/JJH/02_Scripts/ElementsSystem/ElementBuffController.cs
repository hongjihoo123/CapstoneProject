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
        }

        [Header("Event")]
        [SerializeField] private EventChannelSO systemChannel;

        [Header("Combos")]
        [SerializeField]
        private List<ElementCombo> combos = new()
        {
            new ElementCombo
            {
                name = "불-물-바람-전기",
                sequence = new[]
                {
                    ElementType.Fire,
                    ElementType.Water,
                    ElementType.Wind,
                    ElementType.Electric
                }
            },
            new ElementCombo
            {
                name = "전기-전기-불",
                sequence = new[]
                {
                    ElementType.Electric,
                    ElementType.Electric,
                    ElementType.Fire
                }
            },
            new ElementCombo
            {
                name = "물-땅",
                sequence = new[]
                {
                    ElementType.Water,
                    ElementType.Earth
                }
            },
            new ElementCombo
            {
                name = "바람",
                sequence = new[]
                {
                    ElementType.Wind
                }
            }
        };

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

                systemChannel.RaiseEvent(
                    SystemEvents.ElementComboNextEvent
                        .Init(
                            nextElement,
                            nextCombo.name,
                            progress,
                            nextCombo.sequence.Length));
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
                    int start = 0;

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

        private void ApplyCombo(ElementCombo combo)
        {
            Debug.Log($"[ElementBuff] Combo: {combo.name}");
        }

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