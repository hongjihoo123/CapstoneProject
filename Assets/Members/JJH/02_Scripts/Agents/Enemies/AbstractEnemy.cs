using Members.JJH._02_Scripts.Agents.Enemies.BT;
using Members.JJH._02_Scripts.Agents.Enemies.BT.Channels;
using Members.JJH._02_Scripts.Agents.Enemies.SkillSystem;
using Members.JJH._02_Scripts.Agents.Modules;
using System.Collections.Generic;
using Unity.Behavior;
using Unity.Behavior.GraphFramework;
using UnityEngine;

namespace Members.JJH._02_Scripts.Agents.Enemies
{
    public abstract class AbstractEnemy : Agent
    {
        [Header("Enemy Data")]
        [field: SerializeField] public EnemyDataSO EnemyData { get; private set; }

        [SerializeField] private BTBlackboardTableSO btTableSO;

        public IEnemySkillModule SkillModule { get; private set; }
        public INavMesh NavMeshAgent { get; private set; }

        protected BehaviorGraphAgent BehaviorAgent { get; private set; }

        private IReadOnlyDictionary<string, SerializableGUID> _guids;
        private BlackboardVariable<StateChannel> _stateChannel;

        protected override void InitializeModules()
        {
            base.InitializeModules();

            SkillModule = GetModule<IEnemySkillModule>();
            Debug.Assert(SkillModule != null, $"{gameObject.name}에는 IEnemySkillModule모듈이 필요합니다.");
            NavMeshAgent = GetModule<INavMesh>();
            Debug.Assert(NavMeshAgent != null, $"{gameObject.name}에는 INavMesh모듈이 필요합니다.");
            BehaviorAgent = GetComponent<BehaviorGraphAgent>();
            Debug.Assert(BehaviorAgent != null, $"{gameObject.name}에는 BehaviorGraphAgent가 필요합니다.");
        }

        private void Start()
        {
            if (btTableSO != null)
            {
                _guids = btTableSO.Map;
            }

            GetBTVariable(BTVars.StateChannel, out _stateChannel);
            SetBTVariable(BTVars.Enemy, this);

            Health.InitHealth(EnemyData.Health);
            NavMeshAgent.SetNavMeshAgent(EnemyData.Speed, EnemyData.AngularSpeed,
                                                                 EnemyData.Acceleration);
        }

        public void HandleOnDead()
        {
            _stateChannel.Value.SendEventMessage(EnemyState.DEAD);
        }

        public virtual void Attack() { }

        #region BT Helper

        protected bool SetBTVariable<T>(string varName, T value)
        {
            if (_guids != null && _guids.TryGetValue(varName, out var guid))
                return BehaviorAgent.SetVariableValue(guid, value);

            Debug.LogError($"BT변수 미해결 : {varName}");
            return false;
        }

        protected bool GetBTVariable<T>(string varName, out BlackboardVariable<T> variable)
        {
            if (_guids != null && _guids.TryGetValue(varName, out var guid))
                return BehaviorAgent.GetVariable(guid, out variable);

            Debug.LogError($"BT변수 미해결 : {varName}");
            variable = null;
            return false;
        }

        #endregion
    }
}