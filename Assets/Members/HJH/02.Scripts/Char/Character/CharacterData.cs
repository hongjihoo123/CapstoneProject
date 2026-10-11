using System;
using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using Members.KYR._01_Scripts.Stats;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Character
{
    // One playable character: identity text for the character windows, the weapon kit
    // (weapon + dash/Q/R + passive), and stat differences applied while it is selected.
    [CreateAssetMenu(menuName = "Character/Character Data")]
    public class CharacterData : ScriptableObject
    {
        [Serializable]
        public struct StatBonus
        {
            public PlayerStatId stat;
            public StatModifierType type;
            [Tooltip("PercentAdd: 0.15 = +15%.")] public float value;
        }

        [SerializeField] private string displayName;
        [SerializeField] private string role;
        [SerializeField, TextArea] private string description;
        [SerializeField] private Sprite portrait;
        [SerializeField] private Color themeColor = Color.white;
        [SerializeField] private WeaponKitData kit;
        [SerializeField] private StatBonus[] statBonuses;

        [Header("Presentation")]
        [SerializeField, Tooltip("Flash + sparks where each hit lands. Off = the weapon's own effect (e.g. a blade) carries the hit.")]
        private bool hitParticles = true;
        [SerializeField, Tooltip("Burst when an enemy dies.")]
        private bool killBurst = true;

        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
        public string Role => role;
        public string Description => description;
        public Sprite Portrait => portrait;
        public Color ThemeColor => themeColor;
        public WeaponKitData Kit => kit;
        public StatBonus[] StatBonuses => statBonuses;
        public bool HitParticles => hitParticles;
        public bool KillBurst => killBurst;
    }
}
