using System;

namespace Members.KYR._01.Scripts.Currency
{
    [Serializable]
    public class CurrencySaveData
    { 
        public int metaSoul;
        public EnchantCharacterSave[] progress = Array.Empty<EnchantCharacterSave>();
    }

    [Serializable]
    public class EnchantCharacterSave
    {
        public string characterId;
        public string[] unlocked = Array.Empty<string>();
        public EnchantSelectionSave[] selected = Array.Empty<EnchantSelectionSave>();
    }

    [Serializable]
    public class EnchantSelectionSave
    {
        public string fragmentId;
        public int optionIndex;
    }
}
