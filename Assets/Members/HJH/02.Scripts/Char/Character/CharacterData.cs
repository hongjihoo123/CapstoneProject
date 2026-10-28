using System;
using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using Members.KYR._01_Scripts.Stats;
using Members.KYR._01.Scripts.Stats;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Character
{
    // One playable character: identity text for the character windows, the weapon kit
    // (weapon + dash/RMB/R + passive), and stat differences applied while it is selected.
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

        [Header("모델 (비워두면 캡슐 그대로)")]
        [SerializeField, Tooltip("캐릭터 모델 프리팹. 플레이어 Visual 밑에 생성됨")]
        private GameObject model;
        [SerializeField, Tooltip("상태 이름: Idle, Run, Dash, Attack, PassiveShot, Skill, WeaponSkill, Ultimate (없는 상태는 Idle 로 대체)")]
        private RuntimeAnimatorController animatorController;
        [SerializeField] private Vector3 modelOffset;
        [SerializeField, Tooltip("모델이 앞(+Z)을 안 보면 여기서 돌려 맞추면 됨")]
        private Vector3 modelRotation;
        [SerializeField] private float modelScale = 1f;

        [Header("Presentation")]
        [SerializeField, Tooltip("Flash + sparks where each hit lands. Off = the weapon's own effect (e.g. a blade) carries the hit.")]
        private bool hitParticles = true;
        [SerializeField, Tooltip("Burst when an enemy dies.")]
        private bool killBurst = true;
        [SerializeField, Tooltip("맞았을 때 이펙트 색. 투명(알파 0)이면 테마색 씀. 테마색이 흰색/회색 계열이면 바닥에 묻혀서 따로 정해주는 게 좋음")]
        private Color hitColor = Color.clear;

        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
        public string Role => role;
        public string Description => description;
        public Sprite Portrait => portrait;
        public Color ThemeColor => themeColor;
        public WeaponKitData Kit => kit;
        public StatBonus[] StatBonuses => statBonuses;
        public bool HitParticles => hitParticles;
        public bool KillBurst => killBurst;
        public Color HitColor => hitColor.a > 0f ? hitColor : themeColor;
        public GameObject Model => model;
        public RuntimeAnimatorController AnimatorController => animatorController;
        public Vector3 ModelOffset => modelOffset;
        public Quaternion ModelRotation => Quaternion.Euler(modelRotation);
        public float ModelScale => modelScale;
    }
}
