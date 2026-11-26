using Members.JJH._02_Scripts.Agents.Enemies;
using RobotWeapons;
using Unity.Behavior;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Weapon.Runtime
{
    [DefaultExecutionOrder(1010)]
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
            if (_receiver == null || _enemy == null || _enemy.Health == null)
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
                _behavior.enabled = false;
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
