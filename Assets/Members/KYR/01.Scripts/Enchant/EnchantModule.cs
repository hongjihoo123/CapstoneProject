using System;
using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.Char.Character;
using Members.JJH._02_Scripts.Systems.ModuleSystem;
using Members.KYR._01.Scripts.Currency;
using Members.KYR._01.Scripts.Enchant.So;
using UnityEngine;

namespace Members.KYR._01.Scripts.Enchant
{
    public class EnchantModule : Module
    {
        private sealed class Progress
        {
            public readonly HashSet<string> Unlocked = new();
            public readonly Dictionary<string, int> Selected = new();
        }

        [SerializeField] private EnchantFragment[] fragments = Array.Empty<EnchantFragment>();

        private readonly Dictionary<CharacterData, Progress> _byCharacter = new();
        private readonly Progress _shared = new();

        public event Action<EnchantFragment> FragmentUnlocked;
        public event Action<EnchantFragment, int> OptionSelected;

        public IReadOnlyList<EnchantFragment> Fragments => fragments;

        public bool IsUnlocked(CharacterData character, EnchantFragment fragment)
        {
            return fragment != null && ProgressOf(character).Unlocked.Contains(fragment.FragmentId);
        }

        public int GetSelectedOption(CharacterData character, EnchantFragment fragment)
        {
            if (fragment == null)
                return -1;

            return ProgressOf(character).Selected.TryGetValue(fragment.FragmentId, out int index) ? index : -1;
        }

        public bool Activate(CharacterData character, EnchantFragment fragment)
        {
            if (fragment == null || string.IsNullOrEmpty(fragment.FragmentId))
                return false;

            Progress progress = ProgressOf(character);
            if (!progress.Unlocked.Add(fragment.FragmentId))
                return false;

            FragmentUnlocked?.Invoke(fragment);
            return true;
        }

        public bool TryUnlock(CharacterData character, EnchantFragment fragment)
        {
            if (!Contains(fragment) || string.IsNullOrEmpty(fragment.FragmentId))
                return false;

            Progress progress = ProgressOf(character);
            if (progress.Unlocked.Contains(fragment.FragmentId))
                return false;

            CurrencyService currency = GameBootstrap.Currency;
            if (currency == null || !currency.TrySpend(CurrencyType.MetaSoul, fragment.UnlockCost))
                return false;

            progress.Unlocked.Add(fragment.FragmentId);
            FragmentUnlocked?.Invoke(fragment);
            return true;
        }

        public bool TrySelectOption(CharacterData character, EnchantFragment fragment, int optionIndex)
        {
            if (!IsUnlocked(character, fragment))
                return false;
            if (fragment.Options == null || optionIndex < 0 || optionIndex >= fragment.Options.Length)
                return false;
            if (fragment.Options[optionIndex] == null)
                return false;

            Progress progress = ProgressOf(character);
            progress.Selected[fragment.FragmentId] = optionIndex;
            OptionSelected?.Invoke(fragment, optionIndex);
            return true;
        }

        private Progress ProgressOf(CharacterData character)
        {
            if (character == null)
                return _shared;

            if (!_byCharacter.TryGetValue(character, out Progress progress))
            {
                progress = new Progress();
                _byCharacter.Add(character, progress);
            }

            return progress;
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