using UnityEngine;

namespace Assets.Members.HJH.Environment
{
    public static class StoneScale
    {
        public const float NoCollider = 0.2f;
        public const float FadeMin = 0.7f;

        public static float Of(Transform t)
        {
            Vector3 s = t.lossyScale;
            return Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
        }

        public static bool WantsCollider(Transform t) => Of(t) > NoCollider;
        public static bool CanFade(Transform t) => Of(t) >= FadeMin;
    }
}
