using UnityEngine;

namespace Assets.Members.HJH.Environment
{
    // 돌 크기(Scale) 기준 한 곳. 씬에 놓인 돌의 Scale(가장 큰 축의 lossyScale)로 판단함.
    //   Scale <= NoCollider            : 콜라이더 없음 (조약돌)
    //   NoCollider < Scale < FadeMin   : 콜라이더 있음, 캐릭터를 가려도 투명해지지 않음
    //   Scale >= FadeMin               : 콜라이더 있음 + 캐릭터를 가리면 투명
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
