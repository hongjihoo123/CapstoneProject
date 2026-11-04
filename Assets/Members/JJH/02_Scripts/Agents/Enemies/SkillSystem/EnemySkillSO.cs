using Members.JJH._02_Scripts.Agents.Enemies.SkillSystem.Skills;
using Members.JJH._02_Scripts.Systems.AnimatorSystem;
using System;
#if UNITY_EDITOR
using UnityEditor;
#endif
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
        [field: SerializeField] public AnimParamSO VFXParam { get; private set; }


#if UNITY_EDITOR
        // MonoScript 는 에디터에만 있음 → 빌드(exe)에서는 아래 skillTypeName 으로 타입을 찾음
        [SerializeField] private MonoScript skillScript;
#endif
        [SerializeField, HideInInspector] private string skillTypeName;
        [NonSerialized] private Type _skillType;

        public Type SkillType => _skillType ??= string.IsNullOrEmpty(skillTypeName) ? null : Type.GetType(skillTypeName);

#if UNITY_EDITOR
        private void OnValidate()
        {
            _skillType = null;
            if (skillScript == null)
            {
                skillTypeName = null;
                return;
            }

            Type type = skillScript.GetClass();

            if (type == null || !typeof(IEnemySkill).IsAssignableFrom(type) || type.IsInterface || type.IsAbstract)
            {
                Debug.LogError($"{name}: EnemySkillSO에 넣는 스크립트는 IEnemySkill을 구현해야 합니다.");
                skillScript = null;
                skillTypeName = null;
                return;
            }

            skillTypeName = type.AssemblyQualifiedName;
        }
#endif
    }
}