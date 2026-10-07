using System;
using Members.KYR._01_Scripts;
using Members.KYR._01_Scripts.Stats;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Assets.Members.HJH._02.Scripts.Char.Character
{
    // Swaps the player's character at runtime: kit (weapon, dash/Q/R, passive) and stat bonuses.
    // Number keys 1..N pick from the roster. E/F (free skills) are kept across switches.
    [DefaultExecutionOrder(-40)]
    public class CharacterSwitcher : MonoBehaviour
    {
        [SerializeField] private PlayerAgent player;
        [SerializeField] private CharacterData[] roster;
        [SerializeField] private int startIndex;
        [SerializeField] private bool numberKeys = true;

        private int _currentIndex = -1;

        public event Action<CharacterData> CharacterChanged;

        public CharacterData Current => _currentIndex >= 0 ? roster[_currentIndex] : null;
        public int CurrentIndex => _currentIndex;
        public CharacterData[] Roster => roster;

        private void Start()
        {
            if (player == null || roster == null || roster.Length == 0)
                return;

            Select(Mathf.Clamp(startIndex, 0, roster.Length - 1));
        }

        private void Update()
        {
            if (!numberKeys || roster == null)
                return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            for (int i = 0; i < roster.Length && i < 9; i++)
            {
                if (keyboard[Key.Digit1 + i].wasPressedThisFrame)
                {
                    Select(i);
                    return;
                }
            }
        }

        public void Select(int index)
        {
            if (player == null || roster == null || index < 0 || index >= roster.Length || roster[index] == null)
                return;

            if (index == _currentIndex)
                return;

            RemoveBonuses(Current);
            _currentIndex = index;

            CharacterData next = roster[index];
            player.EquipKit(next.Kit);
            ApplyBonuses(next);

            CharacterChanged?.Invoke(next);
        }

        private void ApplyBonuses(CharacterData character)
        {
            if (character == null || character.StatBonuses == null || player.Stats == null || player.Stats.Tree == null)
                return;

            foreach (CharacterData.StatBonus bonus in character.StatBonuses)
                player.Stats.AddModifier(bonus.stat, new StatModifier(character, bonus.type, bonus.value));
        }

        private void RemoveBonuses(CharacterData character)
        {
            if (character != null && player.Stats != null && player.Stats.Tree != null)
                player.Stats.RemoveModifiers(character);
        }
    }
}
