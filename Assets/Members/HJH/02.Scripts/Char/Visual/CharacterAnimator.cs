using Assets.Members.HJH._02.Scripts.Char.Character;
using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using Members.KYR._01.Scripts.Player;
using Members.KYR._01.Scripts.Stats;
using RobotWeapons;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Visual
{
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
        private static readonly int Hit = Animator.StringToHash("Hit");
        private static readonly int Dead = Animator.StringToHash("Dead");
        private static readonly int AttackSpeed = Animator.StringToHash("AttackSpeed");
        private static readonly int MoveX = Animator.StringToHash("MoveX");
        private static readonly int MoveY = Animator.StringToHash("MoveY");
        private const string UpperLayerName = "Upper";

        public enum GunHold { None, Idle, Run, Shoot, Aim }
        public GunHold Hold { get; private set; }
        private float _lastShot = -10f;

        [SerializeField, Tooltip("쏘고 나서 이 시간 동안은 견착 자세 유지")] private float shootHold = 0.8f;
        [SerializeField] private float fade = 0.08f;
        [SerializeField, Tooltip("이 속도보다 빠르게 움직이면 Run")] private float runSpeed = 0.2f;
        [SerializeField, Tooltip("이동 방향(MoveX/MoveY) 바뀔 때 부드럽게 따라가는 시간")] private float moveDirDamp = 0.1f;

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
        private bool _hitRequested;
        private bool _dead;
        private IShotInterceptable _gun;
        private RuntimeAnimatorController _checkedController;
        private bool _hasAttackSpeed;
        private bool _hasMoveDir;

        private void Awake()
        {
            _player = GetComponent<PlayerAgent>();
            _model = GetComponent<CharacterModel>();
            _controller = GetComponent<CharacterController>();
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

        public void PlayHit() => _hitRequested = true;

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
                Hold = GunHold.None;
                _base.Reset();
                _upper.Reset();
                return;
            }

            if (animator.runtimeAnimatorController != _checkedController)
                CheckController(animator);

            if (!_player.IsAlive)
            {
                Hold = GunHold.None;
                PlayDead(animator);
                return;
            }
            if (_dead)
            {
                _dead = false;
                _base.Reset();
                _upper.Reset();
            }

            if (_hasAttackSpeed)
                animator.SetFloat(AttackSpeed, CurrentAttackSpeed());
            if (_hasMoveDir)
                UpdateMoveDir(animator);

            int locomotion = _player.Mover.PlanarSpeed > runSpeed ? Run : Idle;
            int action = Action(animator);
            if (action == Attack || action == PassiveShot)
                _lastShot = Time.time;
            _attackRequested = false;
            _passiveShotRequested = false;
            _hitRequested = false;

            bool upperAttack = (action == Attack || action == PassiveShot || action == Hit) && _upper.Layer >= 0 && animator.HasState(_upper.Layer, action);
            if (upperAttack && action == Attack && _base.OneShot && _base.Playing == PassiveShot)
                _base.OneShot = false;
            int body = action != 0 && !upperAttack ? action : locomotion;
            Drive(animator, _base, body, body != locomotion);

            if (_upper.Layer < 0)
            {
                Hold = HoldFor(locomotion, _base.OneShot);
                return;
            }

            bool fullBody = _base.OneShot;
            float weight = animator.GetLayerWeight(_upper.Layer);
            float step = fade > 0f ? Time.deltaTime / fade : 1f;
            animator.SetLayerWeight(_upper.Layer, Mathf.MoveTowards(weight, fullBody ? 0f : 1f, step));
            if (fullBody)
            {
                _upper.Reset();
                Hold = GunHold.None;
                return;
            }
            Drive(animator, _upper, upperAttack ? action : locomotion, upperAttack);
            Hold = HoldFor(locomotion, false);
        }

        private GunHold HoldFor(int locomotion, bool fullBody)
        {
            if (fullBody)
                return GunHold.None;
            if (_upper.Layer >= 0 && _upper.OneShot && _upper.Playing == Hit)
                return GunHold.None;
            bool shooting = _upper.Layer >= 0 && _upper.OneShot && (_upper.Playing == Attack || _upper.Playing == PassiveShot);
            if (shooting)
                return GunHold.Shoot;
            if (Time.time - _lastShot < shootHold)
                return GunHold.Aim;
            return locomotion == Run ? GunHold.Run : GunHold.Idle;
        }

        private void PlayDead(Animator animator)
        {
            _attackRequested = _passiveShotRequested = _hitRequested = false;
            _castRequested = null;
            if (_upper.Layer >= 0)
                animator.SetLayerWeight(_upper.Layer, Mathf.MoveTowards(animator.GetLayerWeight(_upper.Layer), 0f, Time.deltaTime / Mathf.Max(0.01f, fade)));
            if (_dead)
                return;

            _dead = true;
            _upper.Reset();
            if (animator.HasState(0, Dead))
                animator.CrossFadeInFixedTime(Dead, 0.1f, 0, 0f);
            _base.Playing = Dead;
            _base.OneShot = true;
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
            bool busy = _base.OneShot && !Finished(animator, _base.Layer);
            if (_hitRequested && !busy && (animator.HasState(0, Hit) || (_upper.Layer >= 0 && animator.HasState(_upper.Layer, Hit))))
                return Hit;
            return 0;
        }

        private readonly System.Collections.Generic.Dictionary<int, float> _startAt = new();

        private int CastState(Animator animator, SkillData data)
        {
            if (data is not FSM_Skill_Module.ElementSkill.ElementSkillBase element)
                return Skill;
            FSM_Skill_Module.ElementSkill.CastMotion motion = element.Motion;
            if (motion == FSM_Skill_Module.ElementSkill.CastMotion.None)
                return Dash;

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
            if (track.OneShot && !oneShot && !Finished(animator, track.Layer))
                return;

            if (wanted == track.Playing && !(oneShot && (wanted == Attack || wanted == PassiveShot || wanted == Hit)))
                return;

            Play(animator, track, wanted, oneShot);
        }

        private void Play(Animator animator, Track track, int state, bool oneShot)
        {
            if (!animator.HasState(track.Layer, state))
                state = Idle;

            float offset = track == _base || oneShot ? 0f : BaseTimeIn(animator, state);
            if (oneShot && _startAt.TryGetValue(state, out float startAt))
                offset = startAt;
            animator.CrossFadeInFixedTime(state, fade, track.Layer, offset);
            track.Playing = state;
            track.OneShot = oneShot && state != Idle;
        }

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
