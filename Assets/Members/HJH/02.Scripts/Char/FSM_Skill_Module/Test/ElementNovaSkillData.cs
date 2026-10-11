using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.Effects;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.Test
{
    // Test Q/E skill for the swap scene: one blast around the player (or at the cursor when castRange > 0),
    // so swapped skills feel different and have real numbers to show in the tooltip.
    [CreateAssetMenu(menuName = "Skill/Test/Element Nova")]
    public class ElementNovaSkillData : SkillData
    {
        [SerializeField] private float damage = 20f;
        [SerializeField] private float radius = 2.5f;
        [SerializeField, Tooltip("0 = around the player, otherwise the blast lands at the cursor up to this range.")]
        private float castRange;
        [SerializeField] private float hitStop = 0.03f;
        [SerializeField] private float shake = 0.15f;

        public override void Execute(ISkillContext context)
        {
            Vector3 center = context.Transform.position;
            if (castRange > 0f)
            {
                Vector3 direction = SkillAim.Resolve(context, 0f, castRange, out float distance);
                center += direction * distance;
            }

            SkillBlast.Explode(context, center, radius, damage, FxColor, hitStop, shake);
        }

        public override void DescribeStats(List<SkillStat> stats)
        {
            stats.Add(new SkillStat("피해량", $"{damage:0}"));
            stats.Add(new SkillStat("범위", $"{radius:0.#}m"));
            stats.Add(new SkillStat("사거리", castRange > 0f ? $"{castRange:0.#}m" : "자신 주변"));
            base.DescribeStats(stats);
        }
    }
}
