using UnityEngine;

namespace RobotWeapons
{
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
