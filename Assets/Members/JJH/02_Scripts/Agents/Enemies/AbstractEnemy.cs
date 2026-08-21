using Members.JJH._02_Scripts.Agents.Modules;
using Unity.Behavior;
using UnityEngine;

namespace Members.JJH._02_Scripts.Agents.Enemies
{
    public abstract class AbstractEnemy : Agent
    {
        [field: SerializeField] public EnemyDataSO EnemyData { get; private set; }

        public INavMesh EnemyNavMeshAgent { get; private set; }

        protected BehaviorGraphAgent BehaviorAgent { get; private set; }

        protected override void InitializeModules()
        {
            base.InitializeModules();

            EnemyNavMeshAgent = GetModule<INavMesh>();
            Debug.Assert(EnemyNavMeshAgent != null, $"{gameObject.name}에는 INavMeshAgent모듈이 필요합니다.");
            BehaviorAgent = GetComponent<BehaviorGraphAgent>();
            Debug.Assert(BehaviorAgent != null, $"{gameObject.name}에는 BehaviorGraphAgent가 필요합니다.");

            BehaviorAgent.SetVariableValue("Enemy", this);
        }

        public virtual void Attack() { }
    }
}