using Members.JJH._02_Scripts.Agents.Enemies.SkillSystem.Skills;
using Members.JJH._02_Scripts.Systems.ModuleSystem;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Members.JJH._02_Scripts.Agents.Enemies.SkillSystem
{
    public class EnemySkillModule : Module, IEnemySkillModule, IAfterInitModule
    {
        protected Dictionary<Type, IEnemySkill> _skillDict = new Dictionary<Type, IEnemySkill>();

        private AbstractEnemy _enemy;

        public override void Initialize(ModuleOwner owner)
        {
            base.Initialize(owner);

            _enemy = _owner as AbstractEnemy;
            _skillDict = GetComponentsInChildren<IEnemySkill>().ToDictionary(module => module.GetType());
        }

        public void AfterInitalize()
        {
            InitializeComponents();
            AfterInitializeComponents();
        }

        protected virtual void InitializeComponents()
        {
            foreach (IEnemySkill skill in _skillDict.Values)
            {
                skill.Initialize(_enemy);
            }
        }

        protected virtual void AfterInitializeComponents()
        {
            foreach (IAfterInitModule module in _skillDict.Values.OfType<IAfterInitModule>())
            {
                module.AfterInitalize();
            }
        }

        public IEnemySkill GetSkill<T>() where T : IEnemySkill
        {
            _skillDict.TryGetValue(typeof(T), out IEnemySkill skill);
            return skill;
        }

        public void UseSkill<T>() where T : IEnemySkill
        {
            if (_skillDict.TryGetValue(typeof(T), out IEnemySkill skill))
            {
                skill.UseSkill();
                return;
            }

            IEnemySkill findSkill = _skillDict.Values.FirstOrDefault(skillType => skillType is T);
            if (findSkill != null)
                findSkill.UseSkill();
        }
    }
}