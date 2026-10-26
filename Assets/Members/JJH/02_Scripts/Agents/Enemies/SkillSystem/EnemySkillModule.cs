using Assets.Members.HJH._02.Scripts.Char.Visual;
using Members.JJH._02_Scripts.Agents.Enemies.SkillSystem.Skills;
using Members.JJH._02_Scripts.Systems.ModuleSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Members.JJH._02_Scripts.Agents.Enemies.SkillSystem
{
    public class EnemySkillModule : Module, IEnemySkillModule, IAfterInitModule
    {
        public AttackAreaVisualizer AreaVisualizer { get; private set; }

        protected Dictionary<Type, IEnemySkill> _skillDict = new Dictionary<Type, IEnemySkill>();
        private readonly Dictionary<Type, float> _skillCooldowns = new Dictionary<Type, float>();

        private AbstractEnemy _enemy;

        public override void Initialize(ModuleOwner owner)
        {
            base.Initialize(owner);

            _enemy = _owner as AbstractEnemy;

            AreaVisualizer = GetComponent<AttackAreaVisualizer>();
            Debug.Assert(AreaVisualizer != null, $"{gameObject.name} : EnemySkillModule은 AttackAreaVisualizer를 가져야 합니다.");

            _skillDict = GetComponentsInChildren<IEnemySkill>().ToDictionary(module => module.GetType());
        }

        #region Skill Initialize

        public void AfterInitalize()
        {
            InitializeComponents();
            AfterInitializeComponents();
        }

        protected virtual void InitializeComponents()
        {
            foreach (IEnemySkill skill in _skillDict.Values)
            {
                skill.Initialize(_enemy, this);
            }
        }

        protected virtual void AfterInitializeComponents()
        {
            foreach (IAfterInitModule module in _skillDict.Values.OfType<IAfterInitModule>())
            {
                module.AfterInitalize();
            }
        }

        #endregion

        public IEnemySkill GetSkill<T>() where T : IEnemySkill
        {
            _skillDict.TryGetValue(typeof(T), out IEnemySkill skill);
            return skill;
        }

        public void UseSkill(Type skillType)
        {
            IEnemySkill skill = _skillDict.Values.FirstOrDefault(skill => skill.GetType() == skillType);

            if (skill.IsUsing)
                return;

            var skillData = _enemy.EnemyData.EnemySkills.FirstOrDefault(data =>
                                                                    data != null && data.SkillType == skillType);
            if (skillData == null)
                return;

            if (_skillCooldowns.TryGetValue(skillType, out float nextAvailableTime)
                && Time.time < nextAvailableTime)
                return;

            skill.UseSkill();
            _skillCooldowns[skillType] = Time.time + skillData.Cooltime;
        }

        public bool IsSkillUsing()
        {
            return _skillDict.Values.Any(skill => skill.IsUsing);
        }

        public bool HasAvailableSkill(float distance)
        {
            foreach (var data in _enemy.EnemyData.EnemySkills.OrderByDescending(data => data.Range))
            {
                if (data == null || distance > data.Range)
                    continue;

                Type skillType = data.SkillType;
                if (skillType == null || !_skillDict.TryGetValue(skillType, out IEnemySkill skill) || skill.IsUsing)
                    continue;

                if (_skillCooldowns.TryGetValue(skillType, out float nextAvailableTime) && Time.time < nextAvailableTime)
                    continue;

                return true;
            }

            return false;
        }
    }
}