using UnityEngine;

namespace RobotWeapons
{
    // 판정 없는 순수 시각용 총알. 데미지는 이미 레이캐스트로 확정된 상태고,
    // 이건 그 결과를 눈에 보이게 날아가는 척만 함.
    public class DumbBulletVisual : MonoBehaviour
    {
        private Vector3 direction;
        private float speed;

        public void Launch(Vector3 targetPoint, float bulletSpeed)
        {
            direction = (targetPoint - transform.position).normalized;
            speed = bulletSpeed;
            transform.rotation = Quaternion.LookRotation(direction);

            float dist = Vector3.Distance(transform.position, targetPoint);
            Destroy(gameObject, dist / speed + 0.05f);
        }

        private void Update()
        {
            transform.position += direction * speed * Time.deltaTime;
        }
    }
}
