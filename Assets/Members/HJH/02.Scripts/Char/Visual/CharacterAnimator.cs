using Assets.Members.HJH._02.Scripts.Char.Character;
using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using Members.KYR._01_Scripts;
using Members.KYR._01_Scripts.Stats;
using RobotWeapons;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Visual
{
    // FSM 상태 보고 캐릭터 모델 애니메이션 틀어주는 거임. 애니메이터 컨트롤러엔 상태만 있고 화살표(트랜지션)는 없음,
    // 여기서 이름으로 바로 CrossFade 함.
    //
    //   스킬 FSM 에서 스킬 도는 중  -> Space: Dash / 우클릭: WeaponSkill / R: Ultimate / Q,E: Skill
    //   평타 쏘거나 휘두름          -> Attack
    //   패시브가 평타를 바꿔치기함  -> PassiveShot (건슬링어 충전 충격포)
    //   나머지                      -> 움직이면 Run, 아니면 Idle
    //
    // Dash, Attack 같은 한 번짜리 동작은 클립 끝까지 틀고 나서 Idle/Run 으로 돌아감 (중간에 다른 동작 들어오면 바로 바뀜)
    // 컨트롤러에 없는 상태 이름이면 Idle 로 대신함. 캐릭터 모델 생기면 CharacterModel 이 붙여줌
    //
    // 컨트롤러에 있으면 쓰는 것들 (없으면 그냥 위 방식대로 한 레이어로 돎):
    //   MoveX / MoveY (float) : 캐릭터가 보는 방향 기준 이동 방향 (x=오른쪽, y=앞). Run 블렌드트리가 이걸로 8방향 섞음
    //   "Upper" 레이어         : 상체 마스크 레이어. Idle/Run 은 하체(Base)랑 같이 틀고, Attack 은 상체에서만 틀어서
    //                            뛰면서 쏠 수 있게 함. Dash/스킬 같은 전신 동작 중엔 상체 레이어 꺼둠
    public class CharacterAnimator : MonoBehaviour
    {
        private static readonly int Idle = Animator.StringToHash("Idle");
        private static readonly int Run = Animator.StringToHash("Run");
        private static readonly int Dash = Animator.StringToHash("Dash");
        private static readonly int Attack = Animator.StringToHash("Attack");
        private static readonly int Skill = Animator.StringToHash("Skill");
        private static readonly int WeaponSkill = Animator.StringToHash("WeaponSkill");
        private static readonly int Ultimate = Animator.StringToHash("Ultimate");
        private static readonly int PassiveShot = Animator.StringToHash("PassiveShot");
        private static readonly int AttackSpeed = Animator.StringToHash("AttackSpeed");
        private static readonly int MoveX = Animator.StringToHash("MoveX");
        private static readonly int MoveY = Animator.StringToHash("MoveY");
        private const string UpperLayerName = "Upper";

        [SerializeField] private float fade = 0.08f;
        [SerializeField, Tooltip("이 속도보다 빠르게 움직이면 Run")] private float runSpeed = 0.2f;
        [SerializeField, Tooltip("이동 방향(MoveX/MoveY) 바뀔 때 부드럽게 따라가는 시간")] private float moveDirDamp = 0.1f;

        // 레이어 하나에서 지금 뭐 틀고 있는지
        private sealed class Track
        {
            public int Layer;
            public int Playing;
            public bool OneShot;

            public void Reset()
            {
                Playing = 0;
                OneShot = false;
            }
        }

        private PlayerAgent _player;
        private CharacterModel _model;
        private CharacterController _controller;
        private readonly WeaponVisualTracker _weapon = new();
        private readonly Track _base = new() { Layer = 0 };
        private readonly Track _upper = new() { Layer = -1 };
        private bool _attackRequested;
        private bool _passiveShotRequested;
        private IShotInterceptable _gun;
        private RuntimeAnimatorController _checkedController;
        private bool _hasAttackSpeed;
        private bool _hasMoveDir;

        private void Awake()
        {
            _player = GetComponent<PlayerAgent>();
            _model = GetComponent<CharacterModel>();
            _controller = GetComponent<CharacterController>();
            // 애니 이벤트로 쏘는 총은 방아쇠 당길 때(ShotQueued) 클립 틀고, 총알은 클립의 Anim_Fire 프레임에 나감
            _weapon.ShotQueued += () => _attackRequested = true;
            _weapon.ShotFired += _ => _attackRequested |= !_weapon.WaitsForAnimation;
            _weapon.Swung += _ => _attackRequested = true;
        }

        private void OnDisable()
        {
            _weapon.Clear();
            BindGun(null);
            BindSkills(null);
        }

        // 총은 캐릭터 바뀔 때마다 새로 생겨서 매 프레임 확인함
        private void BindGun(IShotInterceptable gun)
        {
            if (ReferenceEquals(gun, _gun))
                return;
            if (_gun != null)
                _gun.ShotIntercepted -= OnShotIntercepted;
            _gun = gun;
            if (_gun != null)
                _gun.ShotIntercepted += OnShotIntercepted;
        }

        private void OnShotIntercepted() => _passiveShotRequested = true;

        // 원소 스킬은 누르는 순간 끝나는 게 많아서(지속 0초) 스킬 상태만 보면 동작을 못 틂 → "스킬 씀" 알림으로 틂
        private SkillStateModule _skills;
        private SkillData _castRequested;

        private void BindSkills(SkillStateModule skills)
        {
            if (ReferenceEquals(skills, _skills))
                return;
            if (_skills != null)
                _skills.SkillUsed -= OnSkillUsed;
            _skills = skills;
            if (_skills != null)
                _skills.SkillUsed += OnSkillUsed;
        }

        private void OnSkillUsed(SkillUsedInfo info)
        {
            if (info.Slot is SkillSlotId.Basic1 or SkillSlotId.Basic2 && info.Data is FSM_Skill_Module.ElementSkill.ElementSkillBase)
                _castRequested = info.Data;
        }

        private void LateUpdate()
        {
            _weapon.Track(_player);
            BindGun(_player.Weapon != null ? _player.Weapon.Weapon as IShotInterceptable : null);
            BindSkills(_player.SkillFsm);

            Animator animator = _model != null ? _model.Animator : null;
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                _base.Reset();
                _upper.Reset();
                return;
            }

            if (animator.runtimeAnimatorController != _checkedController)
                CheckController(animator);

            // 공격 속도 스탯 올라가면 공격 모션도 빨라지게 (컨트롤러에 AttackSpeed 파라미터 있을 때만)
            if (_hasAttackSpeed)
                animator.SetFloat(AttackSpeed, CurrentAttackSpeed());
            if (_hasMoveDir)
                UpdateMoveDir(animator);

            int locomotion = _player.Mover.PlanarSpeed > runSpeed ? Run : Idle;
            int action = Action(animator);
            _attackRequested = false;
            _passiveShotRequested = false;

            // 상체 레이어 있으면 평타는 상체에서만 -> 하체는 계속 Idle/Run
            bool upperAttack = action == Attack && _upper.Layer >= 0 && animator.HasState(_upper.Layer, Attack);
            int body = action != 0 && !upperAttack ? action : locomotion;
            Drive(animator, _base, body, body != locomotion);

            if (_upper.Layer < 0)
                return;

            // 대시/스킬 같은 전신 동작 중엔 상체 레이어 끔 (끝나면 다시 하체에 맞춰 들어감)
            bool fullBody = _base.OneShot;
            float weight = animator.GetLayerWeight(_upper.Layer);
            float step = fade > 0f ? Time.deltaTime / fade : 1f;
            animator.SetLayerWeight(_upper.Layer, Mathf.MoveTowards(weight, fullBody ? 0f : 1f, step));
            if (fullBody)
            {
                _upper.Reset();
                return;
            }
            Drive(animator, _upper, upperAttack ? Attack : locomotion, upperAttack);
        }

        private void CheckController(Animator animator)
        {
            _checkedController = animator.runtimeAnimatorController;
            _hasAttackSpeed = HasFloat(animator, AttackSpeed);
            _hasMoveDir = HasFloat(animator, MoveX) && HasFloat(animator, MoveY);
            _upper.Layer = animator.GetLayerIndex(UpperLayerName);
            _base.Reset();
            _upper.Reset();
        }

        private static bool HasFloat(Animator animator, int hash)
        {
            foreach (AnimatorControllerParameter p in animator.parameters)
            {
                if (p.nameHash == hash && p.type == AnimatorControllerParameterType.Float)
                    return true;
            }
            return false;
        }

        // 실제 이동 방향을 모델 기준으로 바꿔서 넣음. 멈추면 마지막 방향 유지 (어차피 Idle)
        private void UpdateMoveDir(Animator animator)
        {
            if (_controller == null)
                return;

            Vector3 velocity = _controller.velocity;
            velocity.y = 0f;
            if (velocity.sqrMagnitude < runSpeed * runSpeed)
                return;

            Vector3 local = animator.transform.InverseTransformDirection(velocity);
            Vector2 dir = new Vector2(local.x, local.z).normalized;
            animator.SetFloat(MoveX, dir.x, moveDirDamp, Time.deltaTime);
            animator.SetFloat(MoveY, dir.y, moveDirDamp, Time.deltaTime);
        }

        // 지금 틀어야 할 한 번짜리 동작. 없으면 0
        private int Action(Animator animator)
        {
            if (_castRequested != null)
            {
                SkillData data = _castRequested;
                _castRequested = null;
                return CastState(animator, data);
            }

            SkillStateModule skills = _player.SkillFsm;
            if (skills != null && skills.Machine?.Current is GenericSkillState state && !state.IsFinished)
            {
                return state.Slot switch
                {
                    SkillSlotId.Dash => Dash,
                    SkillSlotId.Weapon => WeaponSkill,
                    SkillSlotId.Ultimate => Ultimate,
                    _ => CastState(animator, state.Data)
                };
            }

            if (_passiveShotRequested)
                return PassiveShot;
            if (_attackRequested)
                return Attack;
            return 0;
        }

        // 원소 스킬 몸 동작: 스킬 모양에 맞는 Cast_<동작> 상태 (컨트롤러에 없으면 예전처럼 Skill).
        // 손 뻗는 순간 직전부터 틀도록 시작 시각을 기억해 둠 (스킬은 누르는 즉시 나가니까)
        private readonly System.Collections.Generic.Dictionary<int, float> _startAt = new();

        private int CastState(Animator animator, SkillData data)
        {
            if (data is not FSM_Skill_Module.ElementSkill.ElementSkillBase element)
                return Skill;
            FSM_Skill_Module.ElementSkill.CastMotion motion = element.Motion;
            if (motion == FSM_Skill_Module.ElementSkill.CastMotion.None)
                return Dash;   // 돌진 스킬은 대시 동작

            CastMotionSet.Entry entry = CastMotionSet.Default != null ? CastMotionSet.Default.Of(motion) : null;
            if (entry == null || string.IsNullOrEmpty(entry.state))
                return Skill;
            int hash = Animator.StringToHash(entry.state);
            if (!animator.HasState(0, hash))
                return Skill;
            _startAt[hash] = Mathf.Max(0f, entry.release - entry.lead);
            return hash;
        }

        private void Drive(Animator animator, Track track, int wanted, bool oneShot)
        {
            // 한 번짜리 동작 중이면 클립 끝날 때까지 기다림 (새 동작 들어오면 바로 바뀜)
            if (track.OneShot && !oneShot && !Finished(animator, track.Layer))
                return;

            // 같은 한 번짜리 동작이 또 들어오면(연사 등) 처음부터 다시
            if (wanted == track.Playing && !(oneShot && (wanted == Attack || wanted == PassiveShot)))
                return;

            Play(animator, track, wanted, oneShot);
        }

        private void Play(Animator animator, Track track, int state, bool oneShot)
        {
            if (!animator.HasState(track.Layer, state))
                state = Idle;

            // 상체가 Idle/Run 으로 돌아올 땐 하체랑 같은 지점부터 (안 그러면 팔다리 박자 어긋남)
            float offset = track == _base || oneShot ? 0f : BaseTimeIn(animator, state);
            if (oneShot && _startAt.TryGetValue(state, out float startAt))
                offset = startAt;   // 시전 동작: 준비 동작 앞부분은 건너뛰고 손 뻗기 직전부터
            animator.CrossFadeInFixedTime(state, fade, track.Layer, offset);
            track.Playing = state;
            track.OneShot = oneShot && state != Idle;
        }

        // 하체(Base)가 같은 상태를 틀고 있으면 그 상태의 현재 재생 시간(초), 아니면 0
        private static float BaseTimeIn(Animator animator, int state)
        {
            AnimatorStateInfo info = animator.IsInTransition(0)
                ? animator.GetNextAnimatorStateInfo(0)
                : animator.GetCurrentAnimatorStateInfo(0);
            if (info.shortNameHash != state)
                return 0f;
            return Mathf.Repeat(info.normalizedTime, 1f) * info.length;
        }

        private static bool Finished(Animator animator, int layer)
        {
            if (animator.IsInTransition(layer))
                return false;
            return animator.GetCurrentAnimatorStateInfo(layer).normalizedTime >= 1f;
        }

        private float CurrentAttackSpeed() =>
            _player.Stats != null && _player.Stats.Tree != null ? _player.Stats.Get(PlayerStatId.AttackSpeed) : 1f;
    }
}
