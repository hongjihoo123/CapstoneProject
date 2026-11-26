using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Visual
{
    public enum MoveSurfaceKind
    {
        None,
        Dust,
        Mud,
        Moss,
        Leaf,
        Water
    }

    public struct MoveSurfaceHit
    {
        public MoveSurfaceKind Kind;
        public Color Tint;
        public Vector3 Point;
        public Vector3 Normal;
    }
}
