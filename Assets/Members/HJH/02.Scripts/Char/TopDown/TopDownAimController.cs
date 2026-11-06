using Members.KYR._01_Scripts;
using Members.KYR._01.Scripts.Player;
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

        [Header("바라보는 방향")]
        [SerializeField, Tooltip("켜면: 걸을 땐 걷는 방향, 쏘거나 스킬 쓸 때만 마우스 쪽. 끄면: 항상 마우스 쪽 (예전)")]
        private bool faceMoveDirection = true;
        [SerializeField, Tooltip("마지막으로 쏘거나 스킬 쓴 뒤 이 시간(초) 동안은 계속 마우스 쪽을 봄")]
        private float combatFacingHold = 0.5f;
        [SerializeField, Tooltip("걷는 방향으로 돌 때 속도 배수 (마우스 쪽으로 돌 땐 즉시)")]
        private float moveTurnMultiplier = 1f;

        private float _combatUntil = -999f;
        private bool _inCombat;
        private Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.SkillStateModule _skills;
        private readonly Visual.WeaponVisualTracker _weapon = new();

        public Vector3 AimPoint { get; private set; }

        // 녹화·자동 시연용: 값이 있으면 마우스 대신 이 월드 위치를 조준 (사람이 마우스를 안 움직여도 됨). 평소엔 null
        public static Vector3? ScriptedAimPoint;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => ScriptedAimPoint = null;

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
            _weapon.ShotQueued += MarkCombat;
            _weapon.ShotFired += _ => MarkCombat();
            _weapon.Swung += _ => MarkCombat();
        }

        private void OnDisable()
        {
            BindSkills(null);
            _weapon.Clear();
        }

        private void BindSkills(Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.SkillStateModule skills)
        {
            if (ReferenceEquals(skills, _skills))
                return;
            if (_skills != null)
                _skills.SkillCasting -= OnSkillCasting;
            _skills = skills;
            if (_skills != null)
                _skills.SkillCasting += OnSkillCasting;
        }

        // 스킬 나가기 직전: 몸을 바로 마우스 쪽으로 (몸 앞 기준으로 나가는 연출이 엉뚱한 데로 안 가게). 대시는 제외
        private void OnSkillCasting(Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.SkillUsedInfo info)
        {
            if (info.Slot == Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.SkillSlotId.Dash)
                return;
            MarkCombat();
            SnapToAim();
        }

        private void MarkCombat() => _combatUntil = Time.time + combatFacingHold;

        private void SnapToAim()
        {
            Vector3 toAim = AimPoint - transform.position;
            toAim.y = 0f;
            if (toAim.sqrMagnitude > 0.04f)
                transform.rotation = Quaternion.LookRotation(toAim);
        }

        // 지금 마우스 쪽을 봐야 하나: 사격 누르는 중 / 스킬 조준 중 / 대시 아닌 스킬 도는 중 / 방금 쏘거나 스킬 씀
        private bool InCombat()
        {
            if (player.Input.FireHeld || player.Input.FirePressed)
                MarkCombat();
            if (_skills != null)
            {
                if (_skills.IsAimingSkill)
                    MarkCombat();
                if (_skills.Machine?.Current is Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.GenericSkillState s
                    && !s.IsFinished && s.Slot != Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.SkillSlotId.Dash)
                    MarkCombat();
            }
            return Time.time < _combatUntil;
        }

        private void Update()
        {
            _wasLocked = false;
            if (!player.IsAlive || !player.ControlFsm.IsGameplayAlive)
                return;

            _weapon.Track(player);
            BindSkills(player.SkillFsm);

            if (ScriptedAimPoint.HasValue)
            {
                Vector3 scripted = ScriptedAimPoint.Value;
                AimPoint = new Vector3(scripted.x, transform.position.y + aimPlaneHeight, scripted.z);
            }
            else
            {
                Mouse mouse = Mouse.current;
                if (mouse == null)
                    return;

                Ray ray = viewCamera.ScreenPointToRay(mouse.position.ReadValue());
                var plane = new Plane(Vector3.up, new Vector3(0f, transform.position.y + aimPlaneHeight, 0f));
                if (!plane.Raycast(ray, out float distance))
                    return;

                AimPoint = ray.GetPoint(distance);
            }

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
            // 마우스가 캐릭터 바로 위면 조준 방향은 그대로 둠 (걷는 방향 돌기는 계속)
            bool hasAim = toAim.sqrMagnitude >= 0.04f;
            if (hasAim)
                player.SetAimDirection(toAim.normalized);
            else
                toAim = transform.forward;

            // 대쉬 중엔 날아가는 방향을 봄 (더 빨리 돌아서 짧은 대쉬에도 바로 향함). 끝나면 다시 마우스 쪽.
            // 쏘는 방향(AimDirection)은 그대로 마우스라 대쉬 중에 쏴도 마우스 쪽으로 나감
            Vector3 facing = toAim;
            float speed = turnSpeed;

            // 걸을 땐 걷는 방향, 쏘거나 스킬 쓸 땐 마우스 쪽 (전투 들어가는 순간엔 바로 홱 돌아봄)
            if (faceMoveDirection)
            {
                bool combat = InCombat();
                if (combat && !_inCombat)
                    SnapToAim();
                _inCombat = combat;
                if (!combat)
                {
                    Vector3 move = player.Input.HasMoveInput ? player.Mover.ToWorldMove(player.Input.Move) : Vector3.zero;
                    move.y = 0f;
                    if (move.sqrMagnitude < 0.0001f)
                        facing = transform.forward;   // 가만히 있으면 보던 쪽 그대로
                    else
                        facing = move;
                    speed = turnSpeed * moveTurnMultiplier;
                }
            }

            if (context != null && context.TryGetDashFacing(out Vector3 dash))
            {
                facing = dash;
                speed = turnSpeed * dashTurnMultiplier;
            }
            facing.y = 0f;
            if (facing.sqrMagnitude < 0.0001f)
                return;

            Quaternion target = Quaternion.LookRotation(facing);
            // Normalize: RotateTowards fed its own output every frame drifts off unit length, which the
            // Inspector reports as "QuaternionToEuler: Input quaternion was not normalized".
            transform.rotation = Quaternion.Normalize(Quaternion.RotateTowards(transform.rotation, target, speed * Time.deltaTime));
        }
    }
}
