using Members.JJH._02_Scripts.Agents.Enemies.SkillSystem.Skills;
using Members.JJH._02_Scripts.Systems.AnimatorSystem;
using System;
using UnityEditor;
using UnityEngine;

namespace Members.JJH._02_Scripts.Agents.Enemies.SkillSystem
{
    [CreateAssetMenu(fileName = "Enemy Skill", menuName = "SO/Enemy/Enemy Skill", order = 1)]
    public class EnemySkillSO : ScriptableObject
    {
        [field: SerializeField] public float Cooltime { get; private set; }
        [field: SerializeField] public float PrepareTime { get; private set; }
        [field: SerializeField] public float ExpandTime { get; private set; }
        [field: SerializeField] public float AttackDutation { get; private set; }
        [field: SerializeField] public float Range { get; private set; }
        [field: SerializeField] public float Damage { get; private set; }
        [field: SerializeField] public AnimParamSO AnimParam { get; private set; }


        [SerializeField] private MonoScript skillScript;

        public Type SkillType => skillScript != null ? skillScript.GetClass() : null;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (skillScript == null)
                return;

            Type type = skillScript.GetClass();

            if (type == null || !typeof(IEnemySkill).IsAssignableFrom(type) || type.IsInterface || type.IsAbstract)
            {
                Debug.LogError($"{name}: EnemySkillSO에 넣는 스크립트는 IEnemySkill을 구현해야 합니다.");
                skillScript = null;
            }
        }
#endif
    }
}