using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Members.JJH._02_Scripts.Systems.ModuleSystem
{
    public class ModuleOwner : MonoBehaviour
    {
        protected Dictionary<Type, AbstractModule> _moduleDict;

        protected virtual void Awake()
        {
            _moduleDict = GetComponentsInChildren<AbstractModule>().ToDictionary(module => module.GetType());
            InitializeModules();
            AfterInitializeModules();
        }

        protected virtual void InitializeModules()
        {
            foreach (AbstractModule module in _moduleDict.Values)
            {
                module.Initialize(this);
            }
        }

        protected virtual void AfterInitializeModules()
        {
            foreach (IAfterInitModule module in _moduleDict.Values.OfType<IAfterInitModule>())
            {
                module.AfterInitalize();
            }
        }

        public T GetModule<T>()
        {
            if (_moduleDict.TryGetValue(typeof(T), out AbstractModule module))
            {
                return (T)(object)module;
            }

            AbstractModule findModule = _moduleDict.Values.FirstOrDefault(moduleType => moduleType is T);

            if (findModule is T castedModule)
                return castedModule;

            return default;
        }
    }
}