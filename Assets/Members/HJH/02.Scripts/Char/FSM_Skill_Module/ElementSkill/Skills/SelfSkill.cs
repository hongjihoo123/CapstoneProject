using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.Char.Visual;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.ElementSkill
{
    // 나한테 버프. 버프 도는 동안 몸에 이펙트 붙어 있고, explodeRadius 주면 explodeDelay 뒤에 내 자리에서 터짐
    // 예) 바위 방패 (3초 피해 감소 → 끝날 때 주변 폭발 + 밀치기)
    [CreateAssetMenu(menuName = "Skill/Element/Self (버프)")]
    public class SelfSkill : ElementSkillBase
    {
        [Header("나한테")]
        [SerializeField] private List<SelfEffect> onSelf = new();
        [SerializeField, Tooltip("버프 도는 동안 몸에 붙어 있는 이펙트")] private FxSlot auraFx = new();

        [Header("끝날 때 폭발 (0 = 없음)")]
        [SerializeField] private float explodeDelay = 3f;
        [SerializeField] private float explodeRadius;

        protected override IReadOnlyList<SelfEffect> SelfEffects => onSelf;

        protected override ISkillExecution Cast(ElementSkillCast cast)
        {
            cast.ApplySelf(onSelf);
            Fx.Shockwave(cast.Position, cast.Color, 1.2f);
            cast.Context.PlayShake(0.08f, 0.12f);

            FxHandle aura = cast.Hold(auraFx, cast.Position + Vector3.up, Vector3.forward);
            if (aura != null)
                Fx.FollowFor(aura, cast.Context.Transform, Vector3.up, Mathf.Max(LongestBuff(), explodeDelay, 1f));

            if (explodeRadius > 0f)
                cast.Context.Run(new DelayedImpact(cast, null, explodeDelay, explodeRadius));
            return null;
        }

        private float LongestBuff()
        {
            float longest = 0f;
            foreach (SelfEffect effect in onSelf)
                longest = Mathf.Max(longest, effect.duration);
            return longest;
        }

        public override void DescribePreview(ISkillContext context, ISkillPreview preview)
        {
            if (explodeRadius > 0f)
                preview.Circle(context.Transform.position, explodeRadius);
        }

        protected override void DescribeShape(List<SkillStat> stats)
        {
            if (explodeRadius > 0f)
                stats.Add(new SkillStat("끝날 때 폭발", $"{explodeDelay:0.#}초 뒤 · {explodeRadius:0.#}m"));
        }
    }
}
