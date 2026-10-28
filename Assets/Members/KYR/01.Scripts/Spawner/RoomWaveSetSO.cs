using System.Collections.Generic;
using Members.KYR._01.Scripts.Spawner;
using UnityEngine;

namespace Members.KYR._01_Scripts.Spawner
{
    [CreateAssetMenu(fileName = "Room Wave Set", menuName = "SO/Enemy/Room Wave Set", order = 3)]
    public class RoomWaveSetSO : ScriptableObject
    {
        [SerializeField] private List<EnemyWaveSO> waves = new List<EnemyWaveSO>();

        public IReadOnlyList<EnemyWaveSO> Waves => waves;
    }
}
