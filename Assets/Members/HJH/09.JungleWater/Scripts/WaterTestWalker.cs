using UnityEngine;
using UnityEngine.InputSystem;

namespace Assets.Members.HJH.Environment
{
    // 물 테스트용 임시 캐릭터.
    //  WASD 이동 / Space = 내 자리에 스킬 물튀김 / 마우스 왼쪽 = 클릭한 곳에 물튀김
    [RequireComponent(typeof(CharacterController))]
    public class WaterTestWalker : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float gravity = -20f;
        [SerializeField] private float selfSplashRadius = 2f;
        [SerializeField] private float clickSplashRadius = 1.5f;

        private CharacterController _controller;
        private float _verticalSpeed;

        private void Awake() => _controller = GetComponent<CharacterController>();

        private void Update()
        {
            Camera cam = Camera.main;
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;

            Move(cam, keyboard);

            if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
                JungleWater.Splash(transform.position, selfSplashRadius, 1.2f);

            if (mouse != null && cam != null && mouse.leftButton.wasPressedThisFrame)
            {
                Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
                if (Physics.Raycast(ray, out RaycastHit hit, 200f))
                    JungleWater.Splash(hit.point, clickSplashRadius, 1f);
            }
        }

        private void Move(Camera cam, Keyboard keyboard)
        {
            Vector2 input = Vector2.zero;
            if (keyboard != null)
            {
                if (keyboard.wKey.isPressed) input.y += 1f;
                if (keyboard.sKey.isPressed) input.y -= 1f;
                if (keyboard.dKey.isPressed) input.x += 1f;
                if (keyboard.aKey.isPressed) input.x -= 1f;
            }

            Vector3 forward = cam != null ? Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized : Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 move = Vector3.ClampMagnitude(forward * input.y + right * input.x, 1f) * moveSpeed;

            if (_controller.isGrounded && _verticalSpeed < 0f)
                _verticalSpeed = -2f;
            _verticalSpeed += gravity * Time.deltaTime;

            _controller.Move((move + Vector3.up * _verticalSpeed) * Time.deltaTime);
        }
    }
}
