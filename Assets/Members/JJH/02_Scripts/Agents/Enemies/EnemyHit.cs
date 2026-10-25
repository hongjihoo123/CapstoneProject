using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.Char.Visual;
using RobotWeapons;
using UnityEngine;

namespace Members.JJH._02_Scripts.Agents.Enemies
{
    // 적 공격 판정 + 임시 이펙트 모음
    //  - 범위 안의 IDamageable 중 '적이 아닌 것'(= 플레이어)한테만 데미지 (적끼리 팀킬 X)
    //  - 콜라이더가 여러 개여도 한 대상은 한 번만 맞음
    public static class EnemyHit
    {
        // 적 공격 이펙트 색 (임시)
        public static readonly Color FxColor = new Color(1f, 0.35f, 0.2f);

        private static readonly Collider[] Buffer = new Collider[64];
        private static readonly HashSet<IDamageable> Once = new HashSet<IDamageable>();

        // 원 범위. angle < 360 이면 forward 기준 부채꼴 (angle = 전체 각도)
        // alreadyHit 을 넘기면 거기 있는 대상은 건너뛰고, 맞은 대상을 거기 추가함 (대쉬처럼 여러 프레임 판정할 때)
        public static int Area(AbstractEnemy attacker, Vector3 center, float radius, float damage,
                               Vector3 forward = default, float angle = 360f, HashSet<IDamageable> alreadyHit = null)
        {
            if (radius <= 0f || damage <= 0f)
                return 0;

            HashSet<IDamageable> done = alreadyHit ?? Once;
            if (alreadyHit == null)
                Once.Clear();

            forward.y = 0f;
            bool cone = angle < 360f && forward.sqrMagnitude > 0.0001f;
            GameObject source = attacker != null ? attacker.gameObject : null;

            int count = Physics.OverlapSphereNonAlloc(center + Vector3.up * 0.5f, radius, Buffer,
                                                      ~0, QueryTriggerInteraction.Ignore);
            int hits = 0;

            for (int i = 0; i < count; i++)
            {
                IDamageable target = Buffer[i].GetComponentInParent<IDamageable>();
                if (target == null || target is AbstractEnemy || !target.IsAlive || done.Contains(target))
                    continue;

                Vector3 targetPos = ((Component)target).transform.position;
                Vector3 toTarget = targetPos - center;
                toTarget.y = 0f;

                if (cone && toTarget.sqrMagnitude > 0.0001f && Vector3.Angle(forward, toTarget) > angle * 0.5f)
                    continue;

                done.Add(target);
                target.TakeDamage(damage, source);
                Debug.Log($"[EnemyHit] {(source != null ? source.name : "?")} → {((Component)target).name} {damage:F1} 데미지 (살아있음: {target.IsAlive})");
                Fx.Hit(targetPos + Vector3.up, toTarget, FxColor, true);
                hits++;
            }

            return hits;
        }
    }
}
