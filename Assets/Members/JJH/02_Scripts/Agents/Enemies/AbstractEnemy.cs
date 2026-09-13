using Members.JJH._02_Scripts.Agents.Enemies.BT;
using Members.JJH._02_Scripts.Agents.Enemies.BT.Channels;
using Members.JJH._02_Scripts.Agents.Modules;
using Members.JJH._02_Scripts.Systems.AnimatorSystem;
using RobotWeapons;
using Unity.Behavior;
using UnityEngine;

namespace Members.JJH._02_Scripts.Agents.Enemies
{
    public abstract class AbstractEnemy : Agent, IWeaponOwner
    {
        [Header("Enemy Data")]
        [field: SerializeField] public EnemyDataSO EnemyData { get; private set; }
        [SerializeField] private AnimParamSO moveSpeedParam;

        [Header("Gun Data")]
        [SerializeField] private Transform aimOrigin;
        [SerializeField] private Transform muzzleOrigin;
        [SerializeField] private WeaponData equippedWeaponData;
        [SerializeField] private WeaponHitbox weaponHitbox;

        public Transform AimOrigin => aimOrigin != null ? aimOrigin : transform;
        public Transform MuzzleOrigin => muzzleOrigin != null ? muzzleOrigin : transform;

        public IWeapon Weapon { get; private set; }
        public INavMesh EnemyNavMeshAgent { get; private set; }

        protected BehaviorGraphAgent BehaviorAgent { get; private set; }

        private BlackboardVariable<StateChannel> _stateEvent;
        private bool isDead = false;

        protected override void InitializeModules()
        {
            base.InitializeModules();

            EnemyNavMeshAgent = GetModule<INavMesh>();
            Debug.Assert(EnemyNavMeshAgent != null, $"{gameObject.name}에는 INavMesh모듈이 필요합니다.");
            BehaviorAgent = GetComponent<BehaviorGraphAgent>();
            Debug.Assert(BehaviorAgent != null, $"{gameObject.name}에는 BehaviorGraphAgent가 필요합니다.");

            Renderer.SetFloat(moveSpeedParam.HashValue, EnemyData.Speed);
            Health.InitHealth(EnemyData.Health);
            EnemyNavMeshAgent.SetNavMeshAgent(EnemyData.Speed, EnemyData.AngularSpeed,
                                                                            EnemyData.Acceleration);

            BehaviorAgent.SetVariableValue("Enemy", this);
            BehaviorAgent.GetVariable("StateChannel", out _stateEvent);

            if (equippedWeaponData != null)
            {
                Weapon = WeaponFactory.Create(equippedWeaponData);
                Weapon.Equip(this);
                weaponHitbox?.Init(Weapon);
            }
        }

        private void Update()
        {
            if (IsAlive == false && isDead == false)
            {
                _stateEvent.Value.SendEventMessage(EnemyState.DEAD);
                isDead = true;
            }
        }

        public virtual void Attack() { }

        public void ApplyDamageTo(IDamageable target, float amount, bool isWeakpoint = false)
            => target?.TakeDamage(amount, gameObject);

        public void SetWeaponHitboxActive(bool active)
            => weaponHitbox?.SetActive(active);
    }
}