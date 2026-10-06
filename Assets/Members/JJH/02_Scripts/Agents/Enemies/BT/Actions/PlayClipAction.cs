using Members.JJH._02_Scripts.Agents.Modules;
using Members.JJH._02_Scripts.Systems.AnimatorSystem;
using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using Action = Unity.Behavior.Action;

namespace Members.JJH._02_Scripts.Agents.Enemies.BT.Actions
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(name: "Play Clip", story: "[Enemy] play [Clip] [PlayOnce] [WaitAnimation]", category: "Action", id: "b4daafc212e4747fd42229ca87255710")]
    public partial class PlayClipAction : Action
    {
        [SerializeReference] public BlackboardVariable<AbstractEnemy> Enemy;
        [SerializeReference] public BlackboardVariable<AnimParamSO> Clip;
        [SerializeReference] public BlackboardVariable<bool> PlayOnce;
        [SerializeReference] public BlackboardVariable<bool> WaitAnimation;

        private IRenderer _renderer;

        private int _savedClipHash;

        protected override Status OnStart()
        {
            if (Enemy.Value == null || Enemy.Value.Renderer == null || Clip.Value == null)
                return Status.Failure;

            _renderer = Enemy.Value.Renderer;
            _savedClipHash = Clip.Value.HashValue;

            _renderer.Animator.speed = 1f;
            _renderer.PlayClip(_savedClipHash, 0f, 0.2f, 0);

            if (WaitAnimation.Value)
                return Status.Running;

            return Status.Success;
        }

        protected override Status OnUpdate()
        {
            if (!WaitAnimation.Value)
                return Status.Success;

            AnimatorStateInfo stateInfo = _renderer.Animator.GetCurrentAnimatorStateInfo(0);

            if (stateInfo.shortNameHash == _savedClipHash && stateInfo.normalizedTime >= 1f)
                return Status.Success;

            return Status.Running;
        }
    }
}

