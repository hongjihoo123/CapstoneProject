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
        private readonly HashSet<string> _hydrated = new();

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

            Sync(character, progress);
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
            Sync(character, progress);
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
            Sync(character, progress);
            OptionSelected?.Invoke(fragment, optionIndex);
            return true;
        }

        private Progress ProgressOf(CharacterData character)
        {
            string id = IdOf(character);
            Progress progress;
            if (character == null)
            {
                progress = _shared;
            }
            else if (!_byCharacter.TryGetValue(character, out progress))
            {
                progress = new Progress();
                _byCharacter.Add(character, progress);
            }

            if (_hydrated.Add(id))
                Hydrate(progress, id);

            return progress;
        }

        private static string IdOf(CharacterData character)
        {
            return character != null ? character.name : string.Empty;
        }

        private static void Hydrate(Progress progress, string id)
        {
            CurrencyService currency = GameBootstrap.Currency;
            if (currency == null || !currency.TryReadEnchant(id, out string[] unlocked, out EnchantSelectionSave[] selected))
                return;

            if (unlocked != null)
            {
                for (int i = 0; i < unlocked.Length; i++)
                {
                    if (!string.IsNullOrEmpty(unlocked[i]))
                        progress.Unlocked.Add(unlocked[i]);
                }
            }

            if (selected == null)
                return;

            for (int i = 0; i < selected.Length; i++)
            {
                EnchantSelectionSave pick = selected[i];
                if (pick == null || string.IsNullOrEmpty(pick.fragmentId))
                    continue;

                progress.Selected[pick.fragmentId] = pick.optionIndex;
            }
        }

        private static void Sync(CharacterData character, Progress progress)
        {
            CurrencyService currency = GameBootstrap.Currency;
            if (currency == null)
                return;

            var unlocked = new string[progress.Unlocked.Count];
            progress.Unlocked.CopyTo(unlocked);

            var selected = new EnchantSelectionSave[progress.Selected.Count];
            int index = 0;
            foreach (KeyValuePair<string, int> pair in progress.Selected)
            {
                selected[index++] = new EnchantSelectionSave
                {
                    fragmentId = pair.Key,
                    optionIndex = pair.Value
                };
            }

            currency.WriteEnchant(IdOf(character), unlocked, selected);
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