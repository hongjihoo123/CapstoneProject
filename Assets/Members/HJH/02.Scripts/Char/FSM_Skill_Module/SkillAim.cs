using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module
{
    public static class SkillAim
    {
        public static Vector3 Resolve(ISkillContext context, float minRange, float maxRange, out float distance)
        {
            Vector3 toPoint = context.AimPoint - context.Transform.position;
            toPoint.y = 0f;

            Vector3 direction = toPoint.sqrMagnitude > 0.0001f ? toPoint.normalized : context.AimDirection;
            distance = Mathf.Clamp(toPoint.magnitude, minRange, maxRange);
            return direction;
        }
    }
}
