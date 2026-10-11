using System.Collections.Generic;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Visual
{
    // Parent object for pooled effect instances, and a small ticker for timed helpers
    // (Fx.After delays, hit-reaction punches). Created on first use. Runs on scaled time.
    public class FxRunner : MonoBehaviour
    {
        private static FxRunner _instance;

        private readonly List<FxInstance> _active = new();
        private readonly List<FxInstance> _pending = new();

        public static FxRunner Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new GameObject("[Fx]").AddComponent<FxRunner>();
                return _instance;
            }
        }

        public void Add(FxInstance instance) => _pending.Add(instance);

        private void Update()
        {
            _active.AddRange(_pending);
            _pending.Clear();

            float deltaTime = Time.deltaTime;
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                FxInstance instance = _active[i];
                if (instance.Tick(deltaTime))
                    continue;

                instance.Release();
                _active.RemoveAt(i);
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _instance = null;
    }

    public abstract class FxInstance
    {
        // Return false when finished.
        public abstract bool Tick(float deltaTime);
        public virtual void Release() { }
    }
}
