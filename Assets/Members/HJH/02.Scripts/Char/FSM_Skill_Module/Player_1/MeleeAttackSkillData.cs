using RobotWeapons;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module
{
    [CreateAssetMenu(fileName = "MeleeAttackSkillData", menuName = "Skill/Melee Attack")]
    public class MeleeAttackSkillData : SkillData
    {
        [SerializeField] private float damage = 30f;
        [SerializeField] private GameObject hitEffectPrefab;

        public override void Execute(SkillStateModule owner) { }

        public override void OnAnimationHitEvent(SkillStateModule owner)
        {
            var box = owner.Player.Weapon.SkillOverlapHitbox;
            if (box == null)
            {
                Debug.LogWarning($"{owner.name}의 PlayerWeapon에 Skill Overlap Hitbox가 연결돼있지 않습니다.");
                return;
            }

            foreach (var target in box.Overlap())
            {
                if (target == null || !target.IsAlive)
                    continue;

                if (!owner.TryRegisterHit(target))
                    continue;

                owner.Player.ApplyDamageTo(target, damage, isWeakpoint: false);

                if (hitEffectPrefab != null && target is Component targetComponent)
                    Object.Instantiate(hitEffectPrefab, targetComponent.transform.position, Quaternion.identity);
            }
        }
    }
}