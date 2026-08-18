using UnityEngine;

namespace MemberWorkSpace.JJH._02_Scripts.Systems.ModuleSystem
{
    public abstract class Module : MonoBehaviour
    {
        protected ModuleOwner _owner;
        public virtual void Initialize(ModuleOwner owner)
        {
            _owner = owner;
        }
    }
}