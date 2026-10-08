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
        private struct ElementCombo
        {
            public string name;
            public ElementType[] sequence;
        }

        [SerializeField] private EventChannelSO systemChannel;

        [Tooltip("스택 조합")]
        [SerializeField]
        private ElementCombo[] combos =
        {
            new ElementCombo
            {
                name = "불-물-바람-전기",
                sequence = new[] { ElementType.Fire, ElementType.Water, ElementType.Wind, ElementType.Electric }
            },
            new ElementCombo
            {
                name = "전기-전기-불",
                sequence = new[] { ElementType.Electric, ElementType.Electric, ElementType.Fire }
            },
            new ElementCombo
            {
                name = "물-땅",
                sequence = new[] { ElementType.Water, ElementType.Earth }
            },
            new ElementCombo
            {
                name = "바람",
                sequence = new[] { ElementType.Wind }
            }
        };

        // Read-only view of the combo table for other systems (HJH combo chain / combo UI).
        public int ComboCount => combos != null ? combos.Length : 0;
        public string GetComboName(int index) => combos[index].name;
        public IReadOnlyList<ElementType> GetComboSequence(int index) => combos[index].sequence;

        private readonly int[] _counts = new int[Enum.GetValues(typeof(ElementType)).Length];

        private void OnEnable()
        {
            systemChannel.AddListener<ElementBuffTriggeredEvent>(HandleTriggered);
        }

        private void OnDisable()
        {
            systemChannel.RemoveListener<ElementBuffTriggeredEvent>(HandleTriggered);
        }

        private void HandleTriggered(ElementBuffTriggeredEvent evt)
        {
            Debug.Log($"[ElementBuff] 발동 순서: {string.Join(" → ", evt.Stacks)}");

            if (TryFindCombo(evt.Stacks, out ElementCombo combo))
            {
                ApplyCombo(combo);
                return;
            }

            Array.Clear(_counts, 0, _counts.Length);
            foreach (ElementType element in evt.Stacks)
                _counts[(int)element]++;

            for (int i = 0; i < _counts.Length; i++)
            {
                if (_counts[i] > 0)
                    ApplyElement((ElementType)i, _counts[i]);
            }
        }

        private bool TryFindCombo(IReadOnlyList<ElementType> stacks, out ElementCombo found)
        {
            foreach (ElementCombo combo in combos)
            {
                if (combo.sequence == null || combo.sequence.Length != stacks.Count) continue;

                bool match = true;
                for (int i = 0; i < stacks.Count; i++)
                {
                    if (combo.sequence[i] != stacks[i])
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                {
                    found = combo;
                    return true;
                }
            }

            found = default;
            return false;
        }

        private void ApplyCombo(ElementCombo combo)
        {
            Debug.Log($"[ElementBuff] 조합 발동: {combo.name} ({string.Join(" → ", combo.sequence)})");
        }

        private void ApplyElement(ElementType element, int stackCount)
        {
            Debug.Log($"[ElementBuff] {element} 버프 적용 (x{stackCount})");
        }
    }
}