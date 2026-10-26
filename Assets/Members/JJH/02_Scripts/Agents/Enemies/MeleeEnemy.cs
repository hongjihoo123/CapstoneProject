using Assets.Members.HJH._02.Scripts.Char.Visual;
using UnityEngine;

namespace Members.JJH._02_Scripts.Agents.Enemies
{
    public class MeleeEnemy : AbstractEnemy
    {
        // 일반 공격: 앞쪽 부채꼴(AttackRange, AttackSight 각도) 안의 플레이어 한 대
        public override void NormalAttack()
        {
            if (EnemyData == null)
                return;

            Vector3 forward = transform.forward;
            float range = EnemyData.AttackRange;
            float angle = EnemyData.AttackSight > 0f ? EnemyData.AttackSight : 360f;

            Fx.Slash(transform.position + Vector3.up, forward, range);
            EnemyHit.Area(this, transform.position, range, EnemyData.AttackDamage, forward, angle);
        }
    }
}
