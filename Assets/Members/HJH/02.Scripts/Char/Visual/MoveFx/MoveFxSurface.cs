using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Visual
{
    // 터레인이 아닌 바닥(다리·바위·메쉬 바닥) 콜라이더나 그 부모에 붙이면 그 위의 발 이펙트 종류를 정함.
    //  예) 나무 다리 = None / 돌판 = Dust + 회색 / 진흙 웅덩이 메쉬 = Mud
    // 터레인 위에서는 이게 없어도 바닥 페인트(MoveSurfaceTable)로 자동 판별. 붙어 있으면 이게 우선
    public class MoveFxSurface : MonoBehaviour
    {
        [SerializeField] private MoveSurfaceKind kind = MoveSurfaceKind.Dust;
        [SerializeField, Tooltip("먼지·발자국 색에 곱함. 흰색 = 기본 색 그대로")] private Color tint = Color.white;

        public MoveSurfaceKind Kind => kind;
        public Color Tint => tint;
    }
}
