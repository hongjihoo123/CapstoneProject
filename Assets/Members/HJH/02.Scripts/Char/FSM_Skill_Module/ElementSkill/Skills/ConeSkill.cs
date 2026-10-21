using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.Char.Visual;
using RobotWeapons;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.ElementSkill
{
    // 앞으로 부채꼴 한 번에. 밀치기 넣으면 부채꼴 바깥쪽으로 퍼지면서 밀림
    // 예) 돌풍
    [CreateAssetMenu(menuName = "Skill/Element/Cone (부채꼴)")]
    public class ConeSkill : ElementSkillBase
    {
        [Header("부채꼴")]
        [SerializeField] private float length = 5f;
        [SerializeField, Tooltip("전체 각도")] private float angle = 70f;

        protected override ISkillExecution Cast(ElementSkillCast cast)
        {
            Vector3 apex = cast.Position;
            Vector3 direction = cast.Aim(length, out _);
            cast.PlayCast(direction);
            cast.Recoil(direction, 0.18f);

            AttackAreaBus.Raise(AttackArea.Cone(AttackAreaKind.Skill, apex, Quaternion.LookRotation(direction), length, angle, 0.2f));
            Fx.ConeBlast(apex, direction, length, angle, cast.Color);

            int hits = 0;
            foreach (IDamageable target in cast.Context.OverlapSphere(apex + Vector3.up, length))
            {
                Vector3 toTarget = ElementSkillCast.PositionOf(target, apex) - apex;
                toTarget.y = 0f;
                bool onTop = toTarget.sqrMagnitude <= 0.01f;   // 내 몸이랑 겹친 적은 그냥 맞음
                if (!onTop && Vector3.Angle(direction, toTarget) > angle * 0.5f)
                    continue;   // 부채꼴 밖

                if (cast.Hit(target, onTop ? direction : toTarget))
                    hits++;
            }

            cast.Feel(hits);
            return null;
        }

        public override void DescribePreview(ISkillContext context, ISkillPreview preview)
        {
            Vector3 direction = SkillAim.Resolve(context, 0f, length, out _);
            preview.Cone(context.Transform.position, direction, length, angle);
        }

        protected override void DescribeShape(List<SkillStat> stats)
        {
            stats.Add(new SkillStat("길이", $"{length:0.#}m"));
            stats.Add(new SkillStat("각도", $"{angle:0}°"));
        }
    }
}
