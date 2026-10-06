using RobotWeapons;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module
{
    [CreateAssetMenu(fileName = "MeleeAttackSkillData", menuName = "Skill/Melee Attack")]
    public class MeleeAttackSkillData : SkillData
    {
        [SerializeField] private float damage = 30f;
        [SerializeField] private GameObject hitEffectPrefab;
        [SerializeField] private float bleedDamagePerTick = 5f;
        [SerializeField] private float bleedTickInterval = 1f;
        [SerializeField] private float bleedDuration = 3f;

        public override void Execute(ISkillContext context) { }

        public override void OnAnimationHitEvent(ISkillContext context)
        {
            foreach (IDamageable target in context.OverlapMelee())
            {
                if (target == null || !target.IsAlive)
                    continue;
                if (!context.TryRegisterHit(target))
                    continue;

                context.DealDamage(target, damage);
                context.ApplyDamageOverTime(target, bleedDamagePerTick, bleedTickInterval, bleedDuration);

                if (hitEffectPrefab != null && target is Component targetComponent)
                    Instantiate(hitEffectPrefab, targetComponent.transform.position, Quaternion.identity);
            }
        }
    }
}
