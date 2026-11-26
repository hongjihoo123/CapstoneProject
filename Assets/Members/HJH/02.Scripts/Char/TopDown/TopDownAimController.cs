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

        public static Vector3? ScriptedAimPoint;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => ScriptedAimPoint = null;

        private bool _wasLocked;
        private Vector3 _lockedFacing;

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
            bool hasAim = toAim.sqrMagnitude >= 0.04f;
            if (hasAim)
                player.SetAimDirection(toAim.normalized);
            else
                toAim = transform.forward;

            Vector3 facing = toAim;
            float speed = turnSpeed;

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
                        facing = transform.forward;
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
            transform.rotation = Quaternion.Normalize(Quaternion.RotateTowards(transform.rotation, target, speed * Time.deltaTime));
        }
    }
}
