using Members.JJH._02_Scripts.Agents.Enemies.SkillSystem;
using Members.JJH._02_Scripts.Agents.Enemies.SkillSystem.Skills;
using Members.JJH._02_Scripts.Agents.Enemies.SkillSystem.Skills.BossSkills;
using Members.JJH._02_Scripts.Systems.AnimatorSystem;
using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using Action = Unity.Behavior.Action;

namespace Members.JJH._02_Scripts.Agents.Enemies.BT.Actions
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(name: "Boss use skill", story: "[Boss] use skill", category: "Action/Combat", id: "54b6b19f451d29bf489f482c930d19e8")]
    public partial class BossUseSkillAction : Action
    {
        [SerializeReference] public BlackboardVariable<AbstractEnemy> Boss;
        [Tooltip("실행할 스킬의 AnimParamSO")]
        [SerializeReference] public BlackboardVariable<AnimParamSO> AnimParam;

        private BossEnemy _boss;
        private IEnemySkillModule _skillModule;
        private IEnemySkill _currentSkill;
        private int _skillIndex;

        protected override Status OnStart()
        {
            if (Boss.Value == null || Boss.Value is not BossEnemy boss || Boss.Value.SkillModule == null)
                return Status.Failure;

            _boss = boss;
            _skillModule = _boss.SkillModule;

            if (_boss.Phase == BossPhase.Transition)
            {
                _currentSkill = _skillModule.GetTransitionSkill();
                if (_currentSkill == null || _currentSkill.IsUsing)
                    return Status.Failure;

                AnimParam.Value = _currentSkill.SkillSO.AnimParam;
                _currentSkill.UseSkill();
                return Status.Running;
            }

            var skills = _boss.EnemyData.EnemySkills;
            if (skills == null || skills.Count == 0)
                return Status.Failure;

            for (int i = 0; i < skills.Count; i++)
            {
                int idx = (_skillIndex + i) % skills.Count;
                var data = skills[idx];

                if (data == null || data.SkillType == null)
                    continue;
                if (typeof(ITransitionSkill).IsAssignableFrom(data.SkillType))
                    continue;
                if (!_skillModule.UseSkill(data.SkillType))
                    continue;

                AnimParam.Value = data.AnimParam;
                _skillIndex = idx + 1;
                _currentSkill = null;
                return Status.Running;
            }

            return Status.Failure;
        }

        protected override Status OnUpdate()
        {
            if (_currentSkill != null)
                return _currentSkill.IsUsing ? Status.Running : Status.Success;

            return _skillModule.IsSkillUsing() ? Status.Running : Status.Success;
        }
    }
}