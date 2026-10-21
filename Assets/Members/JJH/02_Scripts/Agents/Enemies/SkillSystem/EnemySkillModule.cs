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

        #endregion

        public IEnemySkill GetSkill<T>() where T : IEnemySkill
        {
            _skillDict.TryGetValue(typeof(T), out IEnemySkill skill);
            return skill;
        }

        public void UseSkill(Type skillType)
        {
            IEnemySkill skill = _skillDict.Values.FirstOrDefault(skill => skill.GetType() == skillType);

            skill?.UseSkill();
        }

        public bool IsSkillUsing()
        {
            return _skillDict.Values.Any(skill => skill.IsUsing);
        }
    }
}