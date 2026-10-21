using System.Collections.Generic;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.ElementSkill
{
    // 원형 한 방. 커서 위치(or 내 주변)에 터짐. 딜레이 주면 바닥 예고 보여주고 잠깐 뒤에 떨어짐
    // 예) 메테오, 낙뢰, 대지의 손아귀
    [CreateAssetMenu(menuName = "Skill/Element/Circle (원형)")]
    public class CircleSkill : ElementSkillBase
    {
        [Header("원형")]
        [SerializeField, Tooltip("켜면 커서 말고 내 주변에서 터짐")] private bool aroundSelf;
        [SerializeField, Tooltip("커서 최대 거리")] private float range = 8f;
        [SerializeField] private float radius = 2f;
        [SerializeField, Tooltip("떨어지기까지 (초). 0 = 바로")] private float delay;
        [SerializeField, Tooltip("떨어지기 전 바닥 예고 이펙트")] private FxSlot windupFx = new();

        protected override ISkillExecution Cast(ElementSkillCast cast)
        {
            Vector3 center = cast.Target(range, aroundSelf);
            cast.PlayCast(center - cast.Position);

            if (delay > 0f)
                cast.Context.Run(new DelayedImpact(cast, center, delay, radius, windupFx));
            else
                cast.Impact(center, radius);
            return null;
        }

        public override void DescribePreview(ISkillContext context, ISkillPreview preview)
        {
            Vector3 position = context.Transform.position;
            if (!aroundSelf)
                preview.RangeCircle(position, range);

            Vector3 direction = SkillAim.Resolve(context, 0f, range, out float distance);
            preview.Circle(aroundSelf ? position : position + direction * distance, radius);
        }

        protected override void DescribeShape(List<SkillStat> stats)
        {
            stats.Add(new SkillStat("범위", $"{radius:0.#}m"));
            stats.Add(new SkillStat("사거리", aroundSelf ? "자신 주변" : $"{range:0.#}m"));
        }
    }
}
