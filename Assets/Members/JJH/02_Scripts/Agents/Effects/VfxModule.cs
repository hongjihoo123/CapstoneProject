using Members.JJH._02_Scripts.Systems.AnimatorSystem;
using Members.JJH._02_Scripts.Systems.ModuleSystem;
using Members.JJH._02_Scripts.Systems.ObjectPoolSystem.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Members.JJH._02_Scripts.Agents.Effects
{
    public class VfxModule : AbstractModule, IVfxModule
    {
        [Serializable]
        private struct VfxInfo
        {
            public AnimParamSO VfxName;
            public PoolItemSO PoolItem;
        }

        [SerializeField] private VfxInfo[] vfxList;

        private Dictionary<int, PoolItemSO> _vfxDict;
        private readonly HashSet<PoolableVfx> _activeVfx = new();
        private PoolInitializer _poolInitializer;

        public override void Initialize(ModuleOwner owner)
        {
            base.Initialize(owner);

            _vfxDict = vfxList.ToDictionary(vfx => vfx.VfxName.HashValue, vfx => vfx.PoolItem);
        }

        public void PlayVfx(int vfxHash, Vector3 position, Quaternion rotation)
        {
            PoolableVfx vfx = PopVfx(vfxHash);
            if (vfx == null) return;

            vfx.OnVfxEnd += HandleVfxEnd;
            _activeVfx.Add(vfx);
            vfx.PlayVfx(position, rotation);
        }

        public void PlayVfx(int vfxHash)
        {
            PoolableVfx vfx = PopVfx(vfxHash);
            if (vfx == null) return;

            vfx.OnVfxEnd += HandleVfxEnd;
            _activeVfx.Add(vfx);
            vfx.PlayVfx();
        }

        private PoolableVfx PopVfx(int vfxHash)
        {
            if (!_vfxDict.TryGetValue(vfxHash, out PoolItemSO poolItem))
            {
                Debug.LogWarning($"VFX with hash {vfxHash} not found");
                return null;
            }

            if (_poolInitializer == null)
                _poolInitializer = FindFirstObjectByType<PoolInitializer>();

            if (_poolInitializer == null)
            {
                Debug.LogError("PoolInitializer not found");
                return null;
            }

            return _poolInitializer.Pop<PoolableVfx>(poolItem);
        }

        private void HandleVfxEnd(PoolableVfx vfx)
        {
            vfx.OnVfxEnd -= HandleVfxEnd;
            _activeVfx.Remove(vfx);
            _poolInitializer.Push(vfx);
        }

        public void StopVfx(int vfxHash)
        {
            foreach (PoolableVfx vfx in _activeVfx.ToArray())
            {
                if (vfx.PlayableVFX.VfxName.HashValue == vfxHash)
                    vfx.StopVfx();
            }
        }

        public void StopAllVfx()
        {
            foreach (PoolableVfx vfx in _activeVfx.ToArray())
                vfx.StopVfx();
        }
    }
}