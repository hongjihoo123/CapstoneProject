using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module
{
    // Shared aiming helpers: cursor direction + clamped distance, and WASD-or-aim direction for dashes.
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

        // Dashes: the WASD direction while moving, otherwise where the player aims.
        public static bool TryMoveDirection(ISkillContext context, out Vector3 direction)
        {
            direction = context.MoveInputDirection;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f)
            {
                direction.Normalize();
                return true;
            }

            direction = context.AimDirection;
            return false;
        }
    }
}
