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

        [SerializeField] private float fade = 0.08f;
        [SerializeField, Tooltip("이 속도보다 빠르게 움직이면 Run")] private float runSpeed = 0.2f;

        private PlayerAgent _player;
        private CharacterModel _model;
        private readonly WeaponVisualTracker _weapon = new();
        private int _playing;
        private bool _oneShot;
        private bool _attackRequested;
        private bool _passiveShotRequested;
        private IShotInterceptable _gun;
        private RuntimeAnimatorController _checkedController;
        private bool _hasAttackSpeed;

        private void Awake()
        {
            _player = GetComponent<PlayerAgent>();
            _model = GetComponent<CharacterModel>();
            _weapon.ShotFired += _ => _attackRequested = true;
            _weapon.Swung += _ => _attackRequested = true;
        }

        private void OnDisable()
        {
            _weapon.Clear();
            BindGun(null);
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

        private void LateUpdate()
        {
            _weapon.Track(_player);
            BindGun(_player.Weapon != null ? _player.Weapon.Weapon as IShotInterceptable : null);

            Animator animator = _model != null ? _model.Animator : null;
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                _playing = 0;
                return;
            }

            // 공격 속도 스탯 올라가면 공격 모션도 빨라지게 (컨트롤러에 AttackSpeed 파라미터 있을 때만)
            if (animator.runtimeAnimatorController != _checkedController)
            {
                _checkedController = animator.runtimeAnimatorController;
                _hasAttackSpeed = false;
                foreach (AnimatorControllerParameter p in animator.parameters)
                    _hasAttackSpeed |= p.nameHash == AttackSpeed;
            }
            if (_hasAttackSpeed)
                animator.SetFloat(AttackSpeed, CurrentAttackSpeed());

            int wanted = Wanted(out bool oneShot);
            _attackRequested = false;
            _passiveShotRequested = false;

            // 한 번짜리 동작 중이면 클립 끝날 때까지 기다림 (새 동작 들어오면 바로 바뀜)
            if (_oneShot && !oneShot && !Finished(animator))
                return;

            // 같은 한 번짜리 동작이 또 들어오면(연사 등) 처음부터 다시
            if (wanted == _playing && !(oneShot && (wanted == Attack || wanted == PassiveShot)))
                return;

            Play(animator, wanted, oneShot);
        }

        private int Wanted(out bool oneShot)
        {
            oneShot = true;

            SkillStateModule skills = _player.SkillFsm;
            if (skills != null && skills.Machine?.Current is GenericSkillState state && !state.IsFinished)
            {
                return state.Slot switch
                {
                    SkillSlotId.Dash => Dash,
                    SkillSlotId.Weapon => WeaponSkill,
                    SkillSlotId.Ultimate => Ultimate,
                    _ => Skill
                };
            }

            if (_passiveShotRequested)
                return PassiveShot;
            if (_attackRequested)
                return Attack;

            oneShot = false;
            return _player.Mover.PlanarSpeed > runSpeed ? Run : Idle;
        }

        private void Play(Animator animator, int state, bool oneShot)
        {
            if (!animator.HasState(0, state))
                state = Idle;

            animator.CrossFadeInFixedTime(state, fade, 0, 0f);
            _playing = state;
            _oneShot = oneShot && state != Idle;
        }

        private static bool Finished(Animator animator)
        {
            if (animator.IsInTransition(0))
                return false;
            return animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1f;
        }

        private float CurrentAttackSpeed() =>
            _player.Stats != null && _player.Stats.Tree != null ? _player.Stats.Get(PlayerStatId.AttackSpeed) : 1f;
    }
}
