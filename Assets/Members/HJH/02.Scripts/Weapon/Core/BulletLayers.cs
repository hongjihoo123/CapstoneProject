using UnityEngine;

namespace RobotWeapons
{
    // 총알·투사체가 닿는 레이어. 이 레이어 말고는 전부 통과함 (보이지 않는 경계 벽 같은 Default 레이어 콜라이더에 안 맞음).
    //   Environment : Obstacle(돌·통나무·벽 같은 장애물), Ground(지형)
    //   Targets     : Enemy, Weakpoint
    // 맵 쪽은 HJH/맵/돌/7 메뉴가 지형=Ground, 돌·통나무·버섯·암벽=Obstacle 로 레이어를 정해 줌.
    public static class BulletLayers
    {
        public static int Environment => LayerMask.GetMask("Obstacle", "Ground");
        public static int Targets => LayerMask.GetMask("Enemy", "Weakpoint");
        public static int All => Environment | Targets;
    }
}
