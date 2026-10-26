using RobotWeapons;
using UnityEngine;

namespace Members.JJH._02_Scripts.Agents.Enemies.SkillSystem.Skills
{
    public abstract class AbstractEnemySkill : MonoBehaviour, IEnemySkill
    {
        [field: SerializeField] public EnemySkillSO SkillSO { get; private set; }

        public bool IsUsing { get; protected set; }

        protected AbstractEnemy _enemy;
        protected IEnemySkillModule _skillModule;

        public virtual void Initialize(AbstractEnemy owner, EnemySkillModule skillModule)
        {
            _enemy = owner;
            _skillModule = skillModule;
        }

        public virtual void UseSkill()
        {
            if (IsUsing || _enemy == null || SkillSO == null)
                return;
        }

        protected void ApplyDamage(Collider hit)
        {
            if (!hit.TryGetComponent<IDamageable>(out var damageable))
                damageable = hit.GetComponentInParent<IDamageable>();

            if (damageable == null || !damageable.IsAlive)
                return;

            damageable.TakeDamage(SkillSO.Damage, _enemy.gameObject);
        }
    }
}