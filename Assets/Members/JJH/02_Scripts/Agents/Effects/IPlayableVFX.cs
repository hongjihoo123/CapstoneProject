using Members.JJH._02_Scripts.Systems.AnimatorSystem;
using UnityEngine;

namespace Members.JJH._02_Scripts.Agents.Effects
{
    public interface IPlayableVFX
    {
        AnimParamSO VfxName { get; }
        float VfxDuration { get; }

        void PlayVfx(Vector3 position, Quaternion rotation);
        void PlayVfx();
        void StopVfx();
    }
}