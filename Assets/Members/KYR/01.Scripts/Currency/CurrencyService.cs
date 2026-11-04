using System;
using UnityEngine;

namespace Members.KYR._01.Scripts.Currency
{
    public class CurrencyService
    {
        public event Action<CurrencyType, int> OnChanged;

        private readonly int[] _values = new int[Enum.GetValues(typeof(CurrencyType)).Length];

        public int Get(CurrencyType type)
        {
            return _values[(int)type];
        }

        public bool CanAfford(CurrencyType type, int amount)
        {
            return amount >= 0 && Get(type) >= amount;
        }

        public void Add(CurrencyType type, int amount)
        {
            if (amount < 0)
            {
                Debug.LogError($"Add에 음수 전달: {amount}");
                return;
            }

            if (amount == 0)
                return;

            long next = (long)Get(type) + amount;
            Set(type, (int)Math.Min(next, int.MaxValue));
        }

        public bool TrySpend(CurrencyType type, int amount)
        {
            if (amount < 0)
            {
                Debug.LogError($"TrySpend에 음수 전달: {amount}");
                return false;
            }

            if (Get(type) < amount)
                return false;

            if (amount == 0)
                return true;

            Set(type, Get(type) - amount);
            return true;
        }

        public void ResetRun()
        {
            Set(CurrencyType.RunGold, 0);
        }

        private void Set(CurrencyType type, int value)
        {
            if (_values[(int)type] == value)
                return;

            _values[(int)type] = value;
            OnChanged?.Invoke(type, value);
        }
    }
}
