using RobotWeapons;
using UnityEngine;

namespace Members.JJH._02_Scripts.Agents.Enemies.SkillSystem.Skills.BossSkills
{
    public abstract class AbstractBossSkill : AbstractEnemySkill
    {
        protected BossEnemy Boss => _enemy as BossEnemy;

        protected bool IsPhase1 => Boss != null && Boss.Phase == BossPhase.Phase1;
        protected bool IsTransition => Boss != null && Boss.Phase == BossPhase.Transition;
        protected bool IsPhase2 => Boss != null && Boss.Phase == BossPhase.Phase2;

        protected override bool followEnemy => false;

        protected bool TryGetTargetPosition(out Vector3 position)
        {
            position = Vector3.zero;

            Collider[] hits = Physics.OverlapSphere(_enemy.transform.position, SkillSetting.Range,
                _skillModule.TargetLayer);

            IDamageable closest = null;
            float closestSqr = float.MaxValue;

            foreach (Collider hit in hits)
            {
                if (hit.transform.IsChildOf(_enemy.transform))
                    continue;

                if (!hit.TryGetComponent<IDamageable>(out var damageable))
                    damageable = hit.GetComponentInParent<IDamageable>();

                if (damageable == null || !damageable.IsAlive)
                    continue;

                float sqr = (hit.transform.position - _enemy.transform.position).sqrMagnitude;

                if (sqr < closestSqr)
                {
                    closestSqr = sqr;
                    closest = damageable;
                    position = hit.transform.position;
                }
            }

            if (closest != null)
                return true;

            position = Vector3.zero;
            return false;
        }
    }
}