using Members.KYR._01.Scripts.Player;
using UnityEngine;

namespace Members.KYR._01.Scripts
{
    public class MinimapFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private float height = 40f;
        [SerializeField] private float yaw;

        private void LateUpdate()
        {
            if (target == null)
            {
                PlayerAgent player = FindFirstObjectByType<PlayerAgent>();
                if (player == null)
                    return;

                target = player.transform;
            }

            Vector3 position = target.position;
            transform.SetPositionAndRotation(
                new Vector3(position.x, position.y + height, position.z),
                Quaternion.Euler(90f, yaw, 0f));
        }
    }
}
