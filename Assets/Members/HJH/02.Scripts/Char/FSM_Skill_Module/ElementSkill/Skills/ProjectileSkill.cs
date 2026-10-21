using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.Effects;
using Assets.Members.HJH._02.Scripts.Char.Visual;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.ElementSkill
{
    // 커서 방향으로 날아가는 거. 여러 개면 부채꼴로 퍼짐, 터지는 범위 주면 맞은 자리에서 터짐
    // 예) 화염구, 윈드 커터, 물방울 감옥, 전기 그물
    [CreateAssetMenu(menuName = "Skill/Element/Projectile (투사체)")]
    public class ProjectileSkill : ElementSkillBase
    {
        [Header("투사체")]
        [SerializeField] private float range = 9f;
        [SerializeField] private float speed = 18f;
        [SerializeField, Tooltip("투사체 굵기(반지름)")] private float radius = 0.4f;
        [SerializeField, Min(1), Tooltip("여러 개면 부채꼴로 퍼짐. 적 하나는 한 번만 맞음")] private int count = 1;
        [SerializeField, Tooltip("여러 개일 때 퍼지는 각도")] private float spread = 40f;
        [SerializeField, Tooltip("뚫고 지나감")] private bool pierce;
        [SerializeField, Tooltip("0 = 맞은 적만, 아니면 맞은 자리에서 이 범위로 터짐")] private float explodeRadius;
        [SerializeField, Tooltip("날아가는 이펙트. 비우면 원소 색 구슬")] private FxSlot travelFx = new();

        protected override ISkillExecution Cast(ElementSkillCast cast)
        {
            Vector3 aim = cast.Aim(range, out _);
            Vector3 origin = cast.Position + Vector3.up;
            cast.PlayCast(aim);
            cast.Recoil(aim, 0.08f + 0.02f * count);
            cast.Context.PlayShake(0.06f, 0.08f);

            for (int i = 0; i < count; i++)
            {
                float offset = count > 1 ? Mathf.Lerp(-spread * 0.5f, spread * 0.5f, i / (count - 1f)) : 0f;
                Vector3 direction = Quaternion.Euler(0f, offset, 0f) * aim;
                Launch(cast, origin, direction);
            }
            return null;
        }

        private void Launch(ElementSkillCast cast, Vector3 origin, Vector3 direction)
        {
            var settings = new SkillProjectile.Settings
            {
                Speed = speed,
                MaxDistance = range,
                Radius = radius,
                Pierce = pierce,
                Color = cast.Color,
                VisualSize = Mathf.Max(0.3f, radius * 1.4f),
                TravelPrefab = travelFx.prefab,
                TravelColor = cast.TintOf(travelFx)
            };

            cast.Context.Run(new SkillProjectile(cast.Context, origin, direction, settings, OnHit, OnFinish));

            // 적한테 닿았을 때 (터지는 스킬이면 여기선 안 때리고 끝날 때 한 번에)
            void OnHit(RobotWeapons.IDamageable target, Vector3 point)
            {
                if (explodeRadius <= 0f && cast.Hit(target, direction))
                    cast.Feel(1);
            }

            // 다 날아갔거나 / 벽 / 적에 막혔을 때
            void OnFinish(Vector3 point)
            {
                if (explodeRadius > 0f)
                    cast.Impact(point, explodeRadius);
                else
                    Fx.ProjectileImpact(point, direction, cast.Color, Mathf.Max(0.5f, radius * 1.5f), false);
            }
        }

        public override void DescribePreview(ISkillContext context, ISkillPreview preview)
        {
            Vector3 start = context.Transform.position;
            Vector3 direction = SkillAim.Resolve(context, 0f, range, out _);
            if (count > 1)
                preview.Cone(start, direction, range, spread);
            else
                preview.Path(start, start + direction * range, radius * 2f);
            preview.DirectionLine(start, start + direction * range);
        }

        protected override void DescribeShape(List<SkillStat> stats)
        {
            stats.Add(new SkillStat("사거리", $"{range:0.#}m"));
            if (count > 1)
                stats.Add(new SkillStat("개수", $"{count}개 (적 하나당 한 번)"));
            if (pierce)
                stats.Add(new SkillStat("관통", "O"));
            if (explodeRadius > 0f)
                stats.Add(new SkillStat("폭발 범위", $"{explodeRadius:0.#}m"));
        }
    }
}
