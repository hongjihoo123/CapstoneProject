using UnityEngine;

namespace Members.JJH._02_Scripts.Agents.Effects
{
    public interface IVfxModule
    {
        void PlayVfx(int vfxHash);
        void PlayVfx(int vfxHash, Vector3 position, Quaternion rotation);
        void StopVfx(int vfxHash);
        void StopAllVfx();
    }
}