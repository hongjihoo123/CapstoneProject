using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.UI
{
    // 타이틀 배경 장면(TitleBackdrop)에서 "카메라가 서 있을 자리". 이 오브젝트를 옮기고 돌리면 타이틀 구도가 바뀜 (씬 뷰에서 직접 조정)
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
