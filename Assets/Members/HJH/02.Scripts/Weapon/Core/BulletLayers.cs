using UnityEngine;

namespace RobotWeapons
{
    public static class BulletLayers
    {
        public static int Environment => LayerMask.GetMask("Obstacle", "Ground");
        public static int Targets => LayerMask.GetMask("Enemy", "Weakpoint");
        public static int All => Environment | Targets;
    }
}
