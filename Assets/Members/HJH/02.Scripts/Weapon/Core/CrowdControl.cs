using Members.KYR._01.Scripts.Player;
using UnityEngine;

namespace RobotWeapons
{
    public enum CrowdControlType
    {
        Slow,
        Root,
        Stun,
        Knockback
    }

    public interface ICrowdControlTarget
    {
        void ApplySlow(float percent, float duration);
        void ApplyRoot(float duration);
        void ApplyStun(float duration);
        void ApplyKnockback(Vector3 direction, float distance);
        bool Has(CrowdControlType type);
    }

    public static class CrowdControl
    {
        public static ICrowdControlTarget Of(IDamageable target)
        {
            if (target is not Component component || component == null)
                return null;

            if (component.TryGetComponent(out ICrowdControlTarget existing))
                return existing;

            if (component.GetComponent<PlayerAgent>() != null)
                return null;

            return component.gameObject.AddComponent<CrowdControlReceiver>();
        }

        public static bool IsImmovable(Component target)
        {
            if (target == null)
                return false;

            if (target.TryGetComponent(out CrowdControlReceiver receiver) && receiver.EnemyTier == CrowdControlReceiver.Tier.Boss)
                return true;

            return target.GetComponentInParent<Members.JJH._02_Scripts.Agents.Enemies.BossEnemy>() != null;
        }
    }
}
