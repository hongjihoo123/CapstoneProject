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

        [SerializeReference] public BlackboardVariable<EnemySkillSO> EnemySkill;
        [SerializeReference] public BlackboardVariable<AnimParamSO> AnimParam;

        private IEnemySkillModule _skillModule;

        private int skillIndex = 0;

        protected override Status OnStart()
        {
            if (Enemy.Value == null || Enemy.Value.SkillModule == null)
                return Status.Failure;


            _skillModule = Enemy.Value.SkillModule;
            if (EnemySkill == null || EnemySkill.Value == null)
            {
                if (Enemy.Value.EnemyData.EnemySkills == null || Enemy.Value.EnemyData.EnemySkills.Count == 0)
                    return Status.Failure;

                skillIndex %= Enemy.Value.EnemyData.EnemySkills.Count;

                _skillModule.UseSkill(Enemy.Value.EnemyData.EnemySkills[skillIndex].SkillType);
                AnimParam.Value = _skillModule.GetSkillSetting(Enemy.Value.EnemyData.EnemySkills[skillIndex])
                                                                    .AttackAnimParam;

                skillIndex++;
            }
            else
            {
                _skillModule.UseSkill(EnemySkill.Value.SkillType);
                AnimParam.Value = _skillModule.GetSkillSetting(EnemySkill.Value).AttackAnimParam;
            }

            return Status.Running;
        }

        protected override Status OnUpdate()
        {
            return _skillModule.IsSkillUsing()
                            ? Status.Running : Status.Success;
        }
    }
}