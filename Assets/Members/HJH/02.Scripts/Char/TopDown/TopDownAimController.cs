using Members.KYR._01_Scripts;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Assets.Members.HJH._02.Scripts.Char.TopDown
{
    [DefaultExecutionOrder(-50)]
    public class TopDownAimController : MonoBehaviour, RobotWeapons.IAimPointProvider
    {
        [SerializeField] private PlayerAgent player;
        [SerializeField] private Camera viewCamera;
        [SerializeField] private float aimPlaneHeight = 1f;
        [SerializeField] private float turnSpeed = 1080f;
        [SerializeField, Tooltip("대쉬 중 몸 돌리는 속도 배수 (짧은 대쉬에도 바로 날아가는 방향을 보게)")]
        private float dashTurnMultiplier = 3f;

        public Vector3 AimPoint { get; private set; }

        private bool _wasLocked;
        private Vector3 _lockedFacing;

        // 다른 코드(마우스 룩 회전 등)가 Update 에서 몸을 돌려도 잠금 중엔 프레임 끝에서 다시 고정
        private void LateUpdate()
        {
            if (_wasLocked && _lockedFacing.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(_lockedFacing);
        }

        private void Start()
        {
            if (viewCamera == null)
                viewCamera = Camera.main;

            player.Mover.MoveReference = viewCamera.transform;
        }

        private void Update()
        {
            _wasLocked = false;
            if (!player.IsAlive || !player.ControlFsm.IsGameplayAlive)
                return;

            Mouse mouse = Mouse.current;
            if (mouse == null)
                return;

            Ray ray = viewCamera.ScreenPointToRay(mouse.position.ReadValue());
            var plane = new Plane(Vector3.up, new Vector3(0f, transform.position.y + aimPlaneHeight, 0f));
            if (!plane.Raycast(ray, out float distance))
                return;

            AimPoint = ray.GetPoint(distance);

            Vector3 toAim = AimPoint - transform.position;
            toAim.y = 0f;

            var context = player.SkillFsm != null ? player.SkillFsm.Context : null;

            // 조준 잠금 중 (충전 사격 등): 마우스 무시하고 잠근 방향으로 조준·몸 고정
            if (context != null && context.TryGetAimLock(out Vector3 locked))
            {
                float reach = Mathf.Max(toAim.magnitude, 1f);
                AimPoint = new Vector3(transform.position.x, AimPoint.y, transform.position.z) + locked * reach;
                player.SetAimPoint(AimPoint);
                player.SetAimDirection(locked);
                transform.rotation = Quaternion.LookRotation(locked);
                _lockedFacing = locked;
                _wasLocked = true;
                return;
            }

            player.SetAimPoint(AimPoint);
            if (toAim.sqrMagnitude < 0.04f)
                return;

            player.SetAimDirection(toAim.normalized);

            // 대쉬 중엔 날아가는 방향을 봄 (더 빨리 돌아서 짧은 대쉬에도 바로 향함). 끝나면 다시 마우스 쪽.
            // 쏘는 방향(AimDirection)은 그대로 마우스라 대쉬 중에 쏴도 마우스 쪽으로 나감
            Vector3 facing = toAim;
            float speed = turnSpeed;
            if (context != null && context.TryGetDashFacing(out Vector3 dash))
            {
                facing = dash;
                speed = turnSpeed * dashTurnMultiplier;
            }

            Quaternion target = Quaternion.LookRotation(facing);
            // Normalize: RotateTowards fed its own output every frame drifts off unit length, which the
            // Inspector reports as "QuaternionToEuler: Input quaternion was not normalized".
            transform.rotation = Quaternion.Normalize(Quaternion.RotateTowards(transform.rotation, target, speed * Time.deltaTime));
        }
    }
}
