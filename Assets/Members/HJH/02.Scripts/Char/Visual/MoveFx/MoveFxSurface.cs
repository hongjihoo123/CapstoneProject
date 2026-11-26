using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Visual
{
    public class MoveFxSurface : MonoBehaviour
    {
        [SerializeField] private MoveSurfaceKind kind = MoveSurfaceKind.Dust;
        [SerializeField, Tooltip("먼지·발자국 색에 곱함. 흰색 = 기본 색 그대로")] private Color tint = Color.white;

        public MoveSurfaceKind Kind => kind;
        public Color Tint => tint;
    }
}
