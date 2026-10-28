using Members.JJH._02_Scripts.Agents.Enemies.SkillSystem;
using Members.JJH._02_Scripts.Systems.AnimatorSystem;
using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using Action = Unity.Behavior.Action;

namespace Members.JJH._02_Scripts.Agents.Enemies.BT.Actions
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(name: "Enemy use skill", story: "[Enemy] use skill", category: "Action/Combat", id: "81a8129a8b900b36138c07f981e01080")]
    public partial class EnemyUseSkillAction : Action
    {
        [SerializeReference] public BlackboardVariable<AbstractEnemy> Enemy;

        [Tooltip("사용하고 싶은 스킬이 있다면 넣기(없다면 스킬 리스트에 들어간 순서대로 실행")]
        [SerializeReference] public BlackboardVariable<EnemySkillSO> EnemySkill;
        [Tooltip("실행할 스킬의 AnimParamSO")]
        [SerializeReference] public BlackboardVariable<AnimParamSO> AnimParam;

        private IEnemySkillModule _enemySkillModule;

        private int skillIndex = 0;

        protected override Status OnStart()
        {
            if (Enemy.Value == null || Enemy.Value.SkillModule == null)
                return Status.Failure;


            _enemySkillModule = Enemy.Value.SkillModule;
            if (EnemySkill == null || EnemySkill.Value == null)
            {
                if (Enemy.Value.EnemyData.EnemySkills == null || Enemy.Value.EnemyData.EnemySkills.Count == 0)
                    return Status.Failure;

                skillIndex %= Enemy.Value.EnemyData.EnemySkills.Count;

                _enemySkillModule.UseSkill(Enemy.Value.EnemyData.EnemySkills[skillIndex].SkillType);
                AnimParam.Value = Enemy.Value.EnemyData.EnemySkills[skillIndex].AnimParam;

                skillIndex++;
            }
            else
            {
                _enemySkillModule.UseSkill(EnemySkill.Value.SkillType);
                AnimParam.Value = EnemySkill.Value.AnimParam;
            }

            return Status.Running;
        }

        protected override Status OnUpdate()
        {
            return _enemySkillModule.IsSkillUsing()
                            ? Status.Running : Status.Success;
        }
    }
}