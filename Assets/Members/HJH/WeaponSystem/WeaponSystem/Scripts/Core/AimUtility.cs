using UnityEngine;

namespace RobotWeapons
{
    public static class AimUtility
    {
        public static LayerMask IgnoreLayerMask;

        public static Quaternion GetConvergedMuzzleRotation(IWeaponOwner owner, Vector3 aimDirection, float aimRange)
        {
            Vector3 aimPos = owner.AimOrigin.position;
            int mask = ~IgnoreLayerMask.value;
            Vector3 targetPoint = Physics.Raycast(aimPos, aimDirection, out var hit, aimRange, mask, QueryTriggerInteraction.Ignore)
                ? hit.point
                : aimPos + aimDirection * aimRange;

            Vector3 muzzleDir = (targetPoint - owner.MuzzleOrigin.position).normalized;
            return muzzleDir.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(muzzleDir) : owner.MuzzleOrigin.rotation;
        }

        public static Vector3 GetSpreadDirection(Vector3 forward, float spreadDeg)
        {
            if (spreadDeg <= 0f) return forward;

            float angle = Random.Range(0f, spreadDeg);
            float spin = Random.Range(0f, 360f);

            Vector3 reference = Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > 0.99f ? Vector3.right : Vector3.up;
            Vector3 perpendicular = Vector3.Cross(forward, reference).normalized;

            return Quaternion.AngleAxis(spin, forward) * Quaternion.AngleAxis(angle, perpendicular) * forward;
        }
    }
}