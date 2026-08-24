using UnityEngine;

namespace RobotWeapons
{
    // AimOrigin(크로스헤어) 기준으로 조준 방향을 계산하고,
    // 실제 투사체는 MuzzleOrigin(총구)에서 그 조준점을 향해 나가도록 회전값을 만들어줌.
    public static class AimUtility
    {
        public static Quaternion GetConvergedMuzzleRotation(IWeaponOwner owner, Vector3 aimDirection, float aimRange)
        {
            Vector3 aimPos = owner.AimOrigin.position;
            Vector3 targetPoint = Physics.Raycast(aimPos, aimDirection, out var hit, aimRange)
                ? hit.point
                : aimPos + aimDirection * aimRange;

            Vector3 muzzleDir = (targetPoint - owner.MuzzleOrigin.position).normalized;
            return muzzleDir.sqrMagnitude > 0f ? Quaternion.LookRotation(muzzleDir) : owner.MuzzleOrigin.rotation;
        }

        public static Vector3 GetSpreadDirection(Vector3 forward, float spreadDeg)
        {
            if (spreadDeg <= 0f) return forward;

            float angle = Random.Range(0f, spreadDeg);
            float spin = Random.Range(0f, 360f);

            Vector3 perpendicular = Vector3.Cross(forward, Vector3.up).normalized;
            if (perpendicular.sqrMagnitude < 0.001f)
                perpendicular = Vector3.Cross(forward, Vector3.right).normalized;

            return Quaternion.AngleAxis(spin, forward) * Quaternion.AngleAxis(angle, perpendicular) * forward;
        }
    }
}
