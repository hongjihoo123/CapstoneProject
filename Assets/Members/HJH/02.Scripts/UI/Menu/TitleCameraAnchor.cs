using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.UI
{
    public class TitleCameraAnchor : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float fieldOfView = 45f;
        public float FieldOfView => fieldOfView;

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawFrustum(transform.position, fieldOfView, 30f, 0.5f, 16f / 9f);
        }
    }
}
