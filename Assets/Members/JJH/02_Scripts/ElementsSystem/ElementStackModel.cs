using System.Collections.Generic;

namespace Members.JJH._02_Scripts.ElementsSystem
{
    public class ElementStackModel
    {
        private readonly List<ElementType> _stacks = new();

        public int MaxStack { get; }
        public int Count => _stacks.Count;
        public bool IsEmpty => _stacks.Count == 0;
        public bool IsFull => _stacks.Count >= MaxStack;
        public IReadOnlyList<ElementType> Stacks => _stacks;

        public ElementStackModel(int maxStack)
        {
            MaxStack = maxStack < 1 ? 1 : maxStack;
        }

        public bool TryPush(ElementType type)
        {
            if (IsFull)
                return false;

            _stacks.Add(type);

            return true;
        }

        public void Clear() => _stacks.Clear();
    }
}