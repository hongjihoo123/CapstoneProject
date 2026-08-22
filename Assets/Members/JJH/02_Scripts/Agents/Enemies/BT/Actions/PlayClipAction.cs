using Members.JJH._02_Scripts.Systems.AnimatorSystem;
using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using Action = Unity.Behavior.Action;

namespace Members.JJH._02_Scripts.Agents.Enemies.BT.Actions
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(name: "PlayClipAction", story: "[Enemy] play [Clip] [PlayOnce]", category: "Action/Animation", id: "b4daafc212e4747fd42229ca87255710")]
    public partial class PlayClipAction : Action
    {
        [SerializeReference] public BlackboardVariable<AbstractEnemy> Enemy;
        [SerializeReference] public BlackboardVariable<AnimParamSO> Clip;
        [SerializeReference] public BlackboardVariable<bool> PlayOnce;

        private int _savedClipHash;
        private int _oneShotClipHash;

        protected override Status OnStart()
        {
            if (Enemy.Value == null || Enemy.Value.Renderer == null || Clip.Value == null)
                return Status.Failure;

            Enemy.Value.Renderer.Animator.speed = 1f;

            if (PlayOnce.Value)
            {
                _oneShotClipHash = Clip.Value.HashValue;

                Enemy.Value.Renderer.PlayClip(_oneShotClipHash, 0.5f, 0.2f, 0);
                return Status.Running;
            }
            else
            {
                _savedClipHash = Clip.Value.HashValue;

                Enemy.Value.Renderer.PlayClip(_savedClipHash, 0.5f, 0.2f, 0);
                return Status.Success;
            }
        }

        protected override Status OnUpdate()
        {
            if (!PlayOnce.Value)
                return Status.Success;

            Animator animator = Enemy.Value.Renderer.Animator;
            var state = animator.GetCurrentAnimatorStateInfo(0);

            if (animator.IsInTransition(0) ||
                state.shortNameHash != _oneShotClipHash ||
                state.normalizedTime < 1f)
                return Status.Running;

            if (_savedClipHash != 0)
                Enemy.Value.Renderer.PlayClip(_savedClipHash, 0.5f, 0.2f, 0);

            return Status.Success;
        }

        protected override void OnEnd()
        {
            _oneShotClipHash = 0;
        }
    }
}

