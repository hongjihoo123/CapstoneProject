using Members.JJH._02_Scripts.Systems.ModuleSystem;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.UI.HealthBar
{
    // 머리 위 월드 캔버스를 캐릭터 위 고정 높이에 두고 카메라를 보게 함 (위치만 담당).
    // KYR HeadHealthBar 의 위치 부분을 그대로 옮김 → 필드 이름이 같아서 프리팹별 높이 설정(fixedHeight 3 등)이 그대로 이어짐
    [DefaultExecutionOrder(10000)]
    public sealed class WorldBarFollow : MonoBehaviour
    {
        [SerializeField, Tooltip("캐릭터 발 기준 높이")] private float fixedHeight = 2f;
        [SerializeField, Tooltip("월드 기준 추가 오프셋")] private Vector3 worldOffset = Vector3.zero;
        [SerializeField, Tooltip("카메라 기준 오프셋 (x 좌우, y 위아래, z 앞뒤)")] private Vector3 screenOffset = Vector3.zero;

        private Transform _body;
        private Camera _camera;

        private void Awake() => _body = FindBody();

        private void LateUpdate()
        {
            if (_body == null)
                _body = FindBody();
            if (_camera == null)
                _camera = Camera.main;

            Quaternion rotation = _camera != null ? _camera.transform.rotation : transform.rotation;
            Vector3 position = _body.position + Vector3.up * fixedHeight + worldOffset + rotation * screenOffset;
            transform.SetPositionAndRotation(position, rotation);
        }

        private Transform FindBody()
        {
            ModuleOwner owner = GetComponentInParent<ModuleOwner>();
            if (owner != null)
                return owner.transform;
            return transform.parent != null ? transform.parent : transform;
        }
    }
}
