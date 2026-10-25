using System;
using UnityEngine;

namespace Members.KYR._01.Scripts.Currency
{
    public class CurrencyService
    {
        public event Action<CurrencyType, int> OnChanged;

        private int _runGold;
        private int _metaSoul;

        public int Get(CurrencyType type)
        {
            return type == CurrencyType.RunGold ? _runGold : _metaSoul;
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
            if (type == CurrencyType.RunGold)
                _runGold = value;
            else
                _metaSoul = value;

            OnChanged?.Invoke(type, value);
        }
    }
}
