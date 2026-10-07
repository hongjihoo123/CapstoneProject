using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.Effects;
using Assets.Members.HJH._02.Scripts.Char.Visual;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.RuneMage
{
    // Character 3 dash: instant teleport toward the cursor (stops short of walls) and leaves a rune
    // trap at the starting point that explodes after a delay.
    [CreateAssetMenu(menuName = "Skill/Rune Mage/Blink")]
    public class BlinkSkillData : SkillData
    {
        [SerializeField] private float maxDistance = 5f;
        [SerializeField] private float trapDelay = 1f;
        [SerializeField] private float trapRadius = 2.5f;
        [SerializeField] private float trapDamage = 20f;

        protected override Color DefaultFxColor => new(0.55f, 0.75f, 1f);

        public override void Execute(ISkillContext context)
        {
            Vector3 origin = context.Transform.position;
            Vector3 direction = SkillAim.Resolve(context, 0f, maxDistance, out float distance);
            Vector3 destination = FindReachable(context, origin, direction, distance);

            if (context.TryFindGround(destination + Vector3.up * 2f, out Vector3 ground))
                destination = ground;

            Color color = FxColor;
            Fx.BlinkOut(origin, color);

            context.SetScriptedPosition(destination);
            context.CancelDash();

            Fx.BlinkIn(destination, direction, color);
            context.PlayHitFeel(0f, 0.06f);

            context.Run(new DelayedBlast(context, origin, trapDelay, trapRadius, trapDamage, color, 0.04f, 0.2f));
        }

        // Walks the distance back until the body fits, so blinking into a wall lands in front of it.
        private static Vector3 FindReachable(ISkillContext context, Vector3 origin, Vector3 direction, float distance)
        {
            const int steps = 8;
            for (int i = steps; i > 0; i--)
            {
                Vector3 candidate = origin + direction * (distance * i / steps);
                if (!context.SweepEnvironment(origin, candidate))
                    return candidate;
            }

            return origin;
        }
    }
}
