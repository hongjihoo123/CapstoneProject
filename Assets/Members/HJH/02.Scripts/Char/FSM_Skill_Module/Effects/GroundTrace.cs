using System.Collections.Generic;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.Effects
{
    public static class GroundTrace
    {
        public const float StepLength = 0.75f;
        private const float MaxSlope = 1.75f;
        private const float MinHover = 0.4f;
        private const float MaxHover = 2.5f;

        public static float HoverOf(ISkillContext context, Vector3 origin)
        {
            if (!context.TryFindGround(origin, out Vector3 ground))
                return -1f;

            return Mathf.Clamp(origin.y - ground.y, MinHover, MaxHover);
        }

        public static bool Step(ISkillContext context, Vector3 from, Vector3 flatDirection, float distance, float hover,
            out Vector3 next)
        {
            next = from + flatDirection * distance;

            if (hover > 0f)
            {
                float climb = distance * MaxSlope + 0.1f;
                Vector3 probe = next + Vector3.up * climb;
                if (context.TryFindGround(probe, out Vector3 ground))
                {
                    float y = ground.y + hover;
                    if (Mathf.Abs(y - from.y) <= climb)
                        next.y = y;
                }
            }

            if (context.LineBlocked(from, next, out Vector3 wall))
            {
                next = wall;
                return true;
            }

            return false;
        }

        public static bool Trace(ISkillContext context, Vector3 start, Vector3 flatDirection, float range, List<Vector3> points)
        {
            points.Clear();
            points.Add(start);

            float hover = HoverOf(context, start);
            Vector3 position = start;
            float remaining = range;

            while (remaining > 0.0001f)
            {
                float step = Mathf.Min(StepLength, remaining);
                bool blocked = Step(context, position, flatDirection, step, hover, out position);
                points.Add(position);
                if (blocked)
                    return true;

                remaining -= step;
            }

            return false;
        }
    }
}
