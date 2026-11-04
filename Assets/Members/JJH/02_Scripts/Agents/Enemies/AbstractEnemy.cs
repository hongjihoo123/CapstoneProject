using Members.JJH._02_Scripts.Agents.Enemies.BT;
using Members.JJH._02_Scripts.Agents.Enemies.BT.Channels;
using Members.JJH._02_Scripts.Agents.Enemies.SkillSystem;
using Members.JJH._02_Scripts.Agents.Modules;
using Members.JJH._02_Scripts.Systems.EventChannelSystem;
using Members.JJH._02_Scripts.Systems.EventChannelSystem.Events;
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
        [field: SerializeField] public EventChannelSO SystemChannel { get; private set; }

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
            Debug.Assert(SkillModule != null, $"{gameObject.name}???? IEnemySkillModule????? ???????.");
            NavMeshAgent = GetModule<INavMesh>();
            Debug.Assert(NavMeshAgent != null, $"{gameObject.name}???? INavMesh????? ???????.");
            BehaviorAgent = GetComponent<BehaviorGraphAgent>();
            Debug.Assert(BehaviorAgent != null, $"{gameObject.name}???? BehaviorGraphAgent?? ???????.");
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
            NavMeshAgent.SetNavMeshAgent(EnemyData.Speed,
                                                                 EnemyData.Acceleration);
        }

        public void HandleOnDead()
        {
            SkillModule.CancelAllSkills();
            VFXModule?.StopAllVfx();
            _stateChannel.Value.SendEventMessage(EnemyState.DEAD);
        }

        public override void Dead()
        {
            SystemChannel.RaiseEvent(SystemEvents.EnemyKilledEvent.Init(EnemyData.Gold));
            base.Dead();
        }

        public virtual void NormalAttack() { }

        #region BT Helper

        public bool SetBTVariable<T>(string varName, T value)
        {
            if (_guids != null && _guids.TryGetValue(varName, out var guid))
                return BehaviorAgent.SetVariableValue(guid, value);

            Debug.LogError($"BT???? ????? : {varName}");
            return false;
        }

        public bool GetBTVariable<T>(string varName, out BlackboardVariable<T> variable)
        {
            if (_guids != null && _guids.TryGetValue(varName, out var guid))
                return BehaviorAgent.GetVariable(guid, out variable);

            Debug.LogError($"BT???? ????? : {varName}");
            variable = null;
            return false;
        }

        public void StopBehavior()
        {
            if (BehaviorAgent != null)
                BehaviorAgent.End();
        }

        #endregion
    }
}