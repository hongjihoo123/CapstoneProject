using UnityEngine;

namespace MemberWorkSpace.JJH._02_Scripts.Systems.ObjectPoolSystem.Runtime
{
    public interface IPoolable
    {
        public PoolItemSO PoolItem { get; set; }
        public GameObject GameObject { get; }
        public void ResetItem();
    }
}