using UnityEngine;

namespace Members.JJH._02_Scripts.Agents.Enemies
{
    [CreateAssetMenu(fileName = "Enemy Data", menuName = "SO/Enemy/Enemy Data", order = 0)]
    public class EnemyDataSO : ScriptableObject
    {
        [Header("Information")]
        [field: SerializeField] public string EnemyName { get; private set; }
        [field: SerializeField] public float Health { get; private set; }

        [Header("NavMesh")]
        [field: SerializeField] public float Speed { get; private set; }
        [field: SerializeField] public float AngularSpeed { get; private set; }
        [field: SerializeField] public float Acceleration { get; private set; }

        [Header("Attack")]
        [field: SerializeField] public float AttackSight { get; private set; }
        [field: SerializeField] public float DetectRange { get; private set; }
        [field: SerializeField] public float AttackRange { get; private set; }
        [field: SerializeField] public float AttackCooltime { get; private set; }
    }
}