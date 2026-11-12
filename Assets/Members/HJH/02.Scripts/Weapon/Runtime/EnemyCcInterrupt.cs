using Members.JJH._02_Scripts.Agents.Enemies;
using RobotWeapons;
using Unity.Behavior;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Weapon.Runtime
{
    // 적이 기절·밀려남·끌려감(CC)에 걸리면 하던 공격/스킬을 캔슬하는 어댑터 (JJH 적 코드는 안 고침).
    //  걸리는 순간: 스킬 캔슬(텔레그래프 포함) → 적 AI(BT) 일시정지(평타가 안 나감) → 기절/피격 애니메이션
    //  풀리는 순간: AI 를 처음부터 다시 시작 → 끊긴 공격을 이어서 바로 때리지 않고, 다시 접근·조준부터
    //  죽으면: AI 를 바로 다시 켜서 JJH 의 DEAD 처리가 그대로 돌게 함
    // CrowdControlReceiver 가 생길 때(적 프리팹에 이미 있음 / 맞을 때 자동) 같이 붙음 (씬·프리팹에 따로 놓을 것 없음)
    [DefaultExecutionOrder(1010)]   // Receiver(1000) 다음
    public sealed class EnemyCcInterrupt : MonoBehaviour
    {
        private static readonly int StunnedHash = Animator.StringToHash("STUNNED");
        private static readonly int GetHitHash = Animator.StringToHash("GETHIT");
        private static readonly int IdleHash = Animator.StringToHash("IDLE");

        [SerializeField, Tooltip("한 번 끊기면 최소 이 시간(초)은 행동 못 함 (밀려남은 0.15초라 너무 짧아서)")]
        private float minInterruptTime = 0.35f;
        [SerializeField, Tooltip("애니메이션 전환 시간")] private float crossFade = 0.06f;

        private CrowdControlReceiver _receiver;
        private AbstractEnemy _enemy;
        private BehaviorGraphAgent _behavior;
        private bool _interrupted;
        private bool _pausedBehavior;
        private float _holdUntil;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Hook()
        {
            CrowdControlReceiver.Awakened -= Attach;
            CrowdControlReceiver.Awakened += Attach;
        }

        private static void Attach(CrowdControlReceiver receiver)
        {
            if (receiver.GetComponent<AbstractEnemy>() != null && receiver.GetComponent<EnemyCcInterrupt>() == null)
                receiver.gameObject.AddComponent<EnemyCcInterrupt>();
        }

        private void Awake()
        {
            _receiver = GetComponent<CrowdControlReceiver>();
            _enemy = GetComponent<AbstractEnemy>();
            _behavior = GetComponent<BehaviorGraphAgent>();
        }

        private void LateUpdate()
        {
            if (_receiver == null || _enemy == null)
                return;

            if (!_enemy.IsAlive)
            {
                ResumeBehavior(restart: false);
                _interrupted = false;
                return;
            }

            bool blocked = _receiver.IsInterrupted;
            if (blocked && !_interrupted)
                Begin();
            else if (!blocked && _interrupted && Time.time >= _holdUntil)
                End();
        }

        private void Begin()
        {
            _interrupted = true;
            _holdUntil = Time.time + minInterruptTime;

            _enemy.SkillModule?.CancelAllSkills();
            if (_behavior != null && _behavior.enabled)
            {
                _behavior.enabled = false;   // 일시정지: 평타 액션·스킬 사용 액션이 안 돌아감
                _pausedBehavior = true;
            }

            bool stunned = _receiver.Has(CrowdControlType.Stun);
            PlayClip(stunned ? StunnedHash : GetHitHash);
        }

        private void End()
        {
            _interrupted = false;
            PlayClip(IdleHash);
            ResumeBehavior(restart: true);
        }

        private void ResumeBehavior(bool restart)
        {
            if (!_pausedBehavior || _behavior == null)
                return;
            _pausedBehavior = false;
            _behavior.enabled = true;
            if (restart)
                _behavior.Restart();
        }

        private void PlayClip(int hash)
        {
            Animator animator = _enemy.Renderer?.Animator;
            if (animator == null || !animator.isActiveAndEnabled || !animator.HasState(0, hash))
                return;
            _enemy.Renderer.PlayClip(hash, 0f, crossFade, 0);
        }

        private void OnDisable() => ResumeBehavior(restart: false);
    }
}
