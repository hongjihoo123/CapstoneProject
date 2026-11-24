using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Visual
{
    // 발밑 바닥 종류. 종류마다 MoveFootFx 의 프로필(튀는 파티클·발자국·풀잎 자국)이 하나씩 있음
    public enum MoveSurfaceKind
    {
        None,   // 아무것도 안 나옴 (나무 다리 등)
        Dust,   // 마른 흙 → 흙먼지 + 옅은 발자국
        Mud,    // 진흙 → 진흙 튐 + 진한 젖은 발자국
        Moss,   // 이끼·풀 → 풀 조각 튐 + 눌린 자국
        Leaf,   // 낙엽 깔린 바닥 → 잎 튐 + 바닥에 떨어진 잎 자국
        Water   // 얕은 물 → 물방울 튐 (수면 파동은 WaterRippleEmitter 가 따로)
    }

    // 한 번 밟았을 때 알아낸 것
    public struct MoveSurfaceHit
    {
        public MoveSurfaceKind Kind;
        public Color Tint;        // 그 바닥 색 (먼지·발자국 색에 곱함). 모르면 흰색
        public Vector3 Point;     // 발이 닿은 곳
        public Vector3 Normal;
    }
}
