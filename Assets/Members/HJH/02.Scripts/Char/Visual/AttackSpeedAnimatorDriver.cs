using Members.JJH._02_Scripts.Systems.AnimatorSystem;
using Members.KYR._01_Scripts;
using Members.KYR._01_Scripts.Stats;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Visual
{
    // Feeds the AttackSpeed stat into an Animator float, so attack clips play at the same speed
    // the weapon uses for its timings. Animator setup: add a float "AttackSpeed" (default 1) and
    // tick "Multiplier -> AttackSpeed" on every attack state. Skipped silently while the parameter is missing.
    public class AttackSpeedAnimatorDriver : MonoBehaviour
    {
        [SerializeField] private PlayerAgent player;
        [SerializeField, Tooltip("Optional. Falls back to Parameter Name.")]
        private AnimParamSO attackSpeedParam;
        [SerializeField] private string parameterName = "AttackSpeed";

        private Animator _animator;
        private RuntimeAnimatorController _controller;
        private bool _hasParameter;

        private int Hash => attackSpeedParam != null ? attackSpeedParam.HashValue : Animator.StringToHash(parameterName);

        private void Awake()
        {
            if (player == null)
                player = GetComponentInParent<PlayerAgent>();
        }

        private void LateUpdate()
        {
            Animator animator = player != null && player.Renderer != null ? player.Renderer.Animator : null;
            if (animator == null)
                return;

            if (animator != _animator || animator.runtimeAnimatorController != _controller)
                CacheParameter(animator);

            if (_hasParameter)
                animator.SetFloat(Hash, CurrentAttackSpeed());
        }

        private float CurrentAttackSpeed() =>
            player.Stats != null && player.Stats.Tree != null ? player.Stats.Get(PlayerStatId.AttackSpeed) : 1f;

        private void CacheParameter(Animator animator)
        {
            _animator = animator;
            _controller = animator.runtimeAnimatorController;
            _hasParameter = false;

            if (_controller == null)
                return;

            int hash = Hash;
            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (parameter.nameHash == hash && parameter.type == AnimatorControllerParameterType.Float)
                {
                    _hasParameter = true;
                    return;
                }
            }
        }
    }
}
