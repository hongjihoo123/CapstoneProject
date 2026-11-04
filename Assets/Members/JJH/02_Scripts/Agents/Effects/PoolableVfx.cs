using Members.JJH._02_Scripts.Systems.ObjectPoolSystem.Runtime;
using System;
using System.Collections;
using UnityEngine;

namespace Members.JJH._02_Scripts.Agents.Effects
{
    public class PoolableVfx : PoolableMono
    {
        [SerializeField] private GameObject effectObject;

        public IPlayableVFX PlayableVFX => _playableVfx;
        public event Action<PoolableVfx> OnVfxEnd;

        private IPlayableVFX _playableVfx;

        private Coroutine _playCoroutine;

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
            if (_playCoroutine != null)
            {
                StopCoroutine(_playCoroutine);
                _playCoroutine = null;
            }
            _playableVfx?.StopVfx();
        }

        public void PlayVfx(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
            PlayVfx();
        }

        public void PlayVfx()
        {
            if (_playCoroutine != null)
                StopCoroutine(_playCoroutine);
            _playCoroutine = StartCoroutine(PlayVfxCoroutine());
        }

        private IEnumerator PlayVfxCoroutine()
        {
            if (_playableVfx == null) yield break;
            _playableVfx.PlayVfx();
            yield return new WaitForSeconds(_playableVfx.VfxDuration);
            _playCoroutine = null;
            OnVfxEnd?.Invoke(this);
        }
    }
}