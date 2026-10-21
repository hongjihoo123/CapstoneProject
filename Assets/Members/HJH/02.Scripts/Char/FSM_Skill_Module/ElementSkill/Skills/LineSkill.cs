using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.Char.Visual;
using RobotWeapons;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.ElementSkill
{
    // 앞으로 즉시 쭉 뻗는 직선 (벽에서 끊김)
    //  - 기본: 빔처럼 한 줄
    //  - groundBurst 켜면: 땅이 앞쪽으로 차례차례 솟구치는 느낌
    // 예) 물대포, 대지 가르기
    [CreateAssetMenu(menuName = "Skill/Element/Line (직선)")]
    public class LineSkill : ElementSkillBase
    {
        [Header("직선")]
        [SerializeField] private float length = 8f;
        [SerializeField, Tooltip("폭의 절반")] private float halfWidth = 0.7f;
        [SerializeField, Tooltip("켜면 빔 대신 땅이 차례로 솟구침")] private bool groundBurst;

        protected override ISkillExecution Cast(ElementSkillCast cast)
        {
            Vector3 direction = cast.Aim(length, out _);
            Vector3 from = cast.Position;
            Vector3 to = from + direction * length;
            if (cast.Context.LineBlocked(from + Vector3.up, to + Vector3.up, out Vector3 wall))
                to = new Vector3(wall.x, from.y, wall.z);

            cast.PlayCast(direction);
            cast.Recoil(direction, 0.15f);
            AttackAreaBus.Raise(AttackArea.Box(AttackAreaKind.Skill, (from + to) * 0.5f, Quaternion.LookRotation(direction),
                new Vector3(halfWidth * 2f, 1f, Vector3.Distance(from, to)), 0.2f));

            int hits = 0;
            foreach (IDamageable target in cast.Context.OverlapCapsule(from + Vector3.up, to + Vector3.up, halfWidth))
            {
                if (cast.Hit(target, direction))
                    hits++;
            }

            if (groundBurst)
                PlayGroundBurst(cast, from, to, direction);
            else
                PlayBeam(cast, from, to, direction);

            cast.Feel(hits, groundSlam: groundBurst, size: halfWidth * 2f);
            return null;
        }

        private void PlayBeam(ElementSkillCast cast, Vector3 from, Vector3 to, Vector3 direction)
        {
            Fx.Beam(from + Vector3.up, to + Vector3.up, cast.Color, halfWidth * 1.2f, 0.3f);
            Fx.ProjectileImpact(to + Vector3.up, direction, cast.Color, halfWidth * 1.5f, true);
            cast.Play(ImpactFx, cast.Ground(to), direction);
        }

        // 앞쪽으로 갈수록 조금씩 늦게 터짐
        private void PlayGroundBurst(ElementSkillCast cast, Vector3 from, Vector3 to, Vector3 direction)
        {
            int pieces = Mathf.Max(2, Mathf.RoundToInt(Vector3.Distance(from, to) / 1.5f));
            for (int i = 1; i <= pieces; i++)
            {
                Vector3 point = cast.Ground(Vector3.Lerp(from, to, i / (float)pieces));
                Fx.After(i * 0.04f, () =>
                {
                    Fx.GroundSlam(point, cast.Color, halfWidth * 1.3f);
                    cast.Play(ImpactFx, point, direction);
                });
            }
        }

        public override void DescribePreview(ISkillContext context, ISkillPreview preview)
        {
            Vector3 start = context.Transform.position;
            Vector3 direction = SkillAim.Resolve(context, 0f, length, out _);
            preview.Path(start, start + direction * length, halfWidth * 2f);
        }

        protected override void DescribeShape(List<SkillStat> stats)
        {
            stats.Add(new SkillStat("길이", $"{length:0.#}m"));
            stats.Add(new SkillStat("폭", $"{halfWidth * 2f:0.#}m"));
        }
    }
}
