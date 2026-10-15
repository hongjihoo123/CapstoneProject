using System;
using System.Collections.Generic;
using Members.JJH._02_Scripts.Systems.ModuleSystem;
using Members.KYR._01.Scripts.Enchant.So;
using UnityEngine;

namespace Members.KYR._01.Scripts.Enchant
{
    public class EnchantModule : Module
    {
        [SerializeField] private EnchantFragment[] fragments = Array.Empty<EnchantFragment>();
        [SerializeField] private int currency;

        private readonly HashSet<string> _unlocked = new();
        private readonly Dictionary<string, int> _selected = new();

        public event Action<EnchantFragment> FragmentUnlocked;
        public event Action<EnchantFragment, int> OptionSelected;

        public int Currency => currency;
        public IReadOnlyList<EnchantFragment> Fragments => fragments;

        public void AddCurrency(int amount)
        {
            if (amount <= 0)
                return;

            currency += amount;
        }

        public bool IsUnlocked(EnchantFragment fragment)
        {
            return fragment != null && _unlocked.Contains(fragment.FragmentId);
        }

        public int GetSelectedOption(EnchantFragment fragment)
        {
            if (fragment == null)
                return -1;

            return _selected.TryGetValue(fragment.FragmentId, out int index) ? index : -1;
        }

        public bool TryUnlock(EnchantFragment fragment)
        {
            if (!Contains(fragment) || string.IsNullOrEmpty(fragment.FragmentId))
                return false;
            if (_unlocked.Contains(fragment.FragmentId))
                return false;
            if (currency < fragment.UnlockCost)
                return false;

            currency -= fragment.UnlockCost;
            _unlocked.Add(fragment.FragmentId);
            FragmentUnlocked?.Invoke(fragment);
            return true;
        }

        public bool TrySelectOption(EnchantFragment fragment, int optionIndex)
        {
            if (!IsUnlocked(fragment))
                return false;
            if (fragment.Options == null || optionIndex < 0 || optionIndex >= fragment.Options.Length)
                return false;
            if (fragment.Options[optionIndex] == null)
                return false;

            _selected[fragment.FragmentId] = optionIndex;
            OptionSelected?.Invoke(fragment, optionIndex);
            return true;
        }

        private bool Contains(EnchantFragment fragment)
        {
            if (fragment == null || fragments == null)
                return false;

            for (int i = 0; i < fragments.Length; i++)
            {
                if (fragments[i] == fragment)
                    return true;
            }

            return false;
        }
    }
}