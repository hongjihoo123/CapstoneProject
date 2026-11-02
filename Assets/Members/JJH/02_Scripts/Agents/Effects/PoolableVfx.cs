using Members.JJH._02_Scripts.Systems.ObjectPoolSystem.Runtime;
using System;
using System.Collections;
using UnityEngine;

namespace Members.JJH._02_Scripts.Agents.Effects
{
    public class PoolableVfx : PoolableMono
    {
        [SerializeField] private GameObject effectObject;

        private IPlayableVFX _playableVfx;

        public IPlayableVFX PlayableVFX => _playableVfx;
        public event Action<PoolableVfx> OnVfxEnd;

        private void Awake()
        {
            _playableVfx = effectObject.GetComponent<IPlayableVFX>();
        }

        private void OnValidate()
        {
            if (effectObject == null) return;

            if (!effectObject.TryGetComponent(out IPlayableVFX _))
                effectObject = null;
        }

        public override void ResetItem()
        {
            _playableVfx?.StopVfx();
        }

        public void PlayVfx(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
            StartCoroutine(PlayVfxCoroutine());
        }

        public void PlayVfx() => StartCoroutine(PlayVfxCoroutine());

        private IEnumerator PlayVfxCoroutine()
        {
            if (_playableVfx == null) yield break;
            _playableVfx.PlayVfx();
            yield return new WaitForSeconds(_playableVfx.VfxDuration);
            OnVfxEnd?.Invoke(this);
        }
    }
}