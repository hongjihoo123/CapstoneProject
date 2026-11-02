using Members.JJH._02_Scripts.Systems.ModuleSystem;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Members.JJH._02_Scripts.Agents.Effects
{
    public class VfxModule : AbstractModule, IVfxModule
    {
        private Dictionary<int, IPlayableVFX> _vfxDict;

        public override void Initialize(ModuleOwner owner)
        {
            base.Initialize(owner);

            _vfxDict = GetComponentsInChildren<IPlayableVFX>().ToDictionary(vfx => vfx.VfxName.HashValue);
        }

        public void PlayVfx(int vfxHash, Vector3 position, Quaternion rotation)
        {
            if (_vfxDict.TryGetValue(vfxHash, out IPlayableVFX vfx))
            {
                vfx.PlayVfx(position, rotation);
            }
            else
            {
                Debug.LogWarning($"VFX with hash {vfxHash} not found");
            }
        }

        public void PlayVfx(int vfxHash)
        {
            if (_vfxDict.TryGetValue(vfxHash, out IPlayableVFX vfx))
            {
                vfx.PlayVfx(); //제자리에서 재생하는 vfx
            }
            else
            {
                Debug.LogWarning($"VFX with hash {vfxHash} not found");
            }
        }

        public void StopVfx(int vfxHash)
        {
            if (_vfxDict.TryGetValue(vfxHash, out IPlayableVFX vfx))
            {
                vfx.StopVfx();
            }
        }
    }
}