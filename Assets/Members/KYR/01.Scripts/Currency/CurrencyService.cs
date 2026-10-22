using System;
using System.Collections.Generic;
using UnityEngine;

namespace Members.KYR._01.Scripts.Currency
{
    public class CurrencyService
    {
        public event Action<CurrencyType, int> OnChanged;

        private readonly ISaveRepository _repo;
        private readonly CurrencySaveData _data;
        private int _runGold;
        private bool _dirty;

        public CurrencyService(ISaveRepository repo)
        {
            _repo = repo;
            _data = _repo.Load() ?? new CurrencySaveData();
            _data.metaSoul = Mathf.Max(0, _data.metaSoul);
            if (_data.progress == null)
                _data.progress = Array.Empty<EnchantCharacterSave>();
        }

        public int Get(CurrencyType type)
        {
            return type == CurrencyType.RunGold ? _runGold : _data.metaSoul;
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

        public void SaveIfDirty()
        {
            if (!_dirty)
                return;

            _repo.Save(_data);
            _dirty = false;
        }

        public bool TryReadEnchant(string characterId, out string[] unlocked, out EnchantSelectionSave[] selected)
        {
            characterId ??= string.Empty;
            EnchantCharacterSave[] progress = _data.progress;
            if (progress != null)
            {
                for (int i = 0; i < progress.Length; i++)
                {
                    EnchantCharacterSave entry = progress[i];
                    if (entry == null || (entry.characterId ?? string.Empty) != characterId)
                        continue;

                    unlocked = entry.unlocked ?? Array.Empty<string>();
                    selected = entry.selected ?? Array.Empty<EnchantSelectionSave>();
                    return true;
                }
            }

            unlocked = Array.Empty<string>();
            selected = Array.Empty<EnchantSelectionSave>();
            return false;
        }

        public void WriteEnchant(string characterId, string[] unlocked, EnchantSelectionSave[] selected)
        {
            characterId ??= string.Empty;
            var kept = new List<EnchantCharacterSave>();
            EnchantCharacterSave[] progress = _data.progress;
            if (progress != null)
            {
                for (int i = 0; i < progress.Length; i++)
                {
                    EnchantCharacterSave entry = progress[i];
                    if (entry == null || (entry.characterId ?? string.Empty) == characterId)
                        continue;

                    kept.Add(entry);
                }
            }

            kept.Add(new EnchantCharacterSave
            {
                characterId = characterId,
                unlocked = unlocked ?? Array.Empty<string>(),
                selected = selected ?? Array.Empty<EnchantSelectionSave>()
            });
            _data.progress = kept.ToArray();
            _dirty = true;
        }

        private void Set(CurrencyType type, int value)
        {
            if (type == CurrencyType.RunGold)
            {
                _runGold = value;
            }
            else
            {
                _data.metaSoul = value;
                _dirty = true;
            }

            OnChanged?.Invoke(type, value);
        }
    }
}
