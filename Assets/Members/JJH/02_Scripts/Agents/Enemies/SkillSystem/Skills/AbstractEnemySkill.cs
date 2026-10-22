using UnityEngine;

namespace Members.JJH._02_Scripts.Agents.Enemies.SkillSystem.Skills
{
    public abstract class AbstractEnemySkill : MonoBehaviour, IEnemySkill
    {
        public bool IsUsing { get; protected set; }

        protected AbstractEnemy _enemy;
        protected EnemySkillModule _skillModule;

        public virtual void Initialize(AbstractEnemy owner, EnemySkillModule skillModule)
        {
            _enemy = owner;
            _skillModule = skillModule;
        }

        public virtual void UseSkill()
        {
            if (IsUsing || _enemy == null)
                return;
        }
    }
}