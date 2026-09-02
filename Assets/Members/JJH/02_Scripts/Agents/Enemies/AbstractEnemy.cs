using Members.JJH._02_Scripts.Agents.Modules;
using RobotWeapons;
using Unity.Behavior;
using UnityEngine;

namespace Members.JJH._02_Scripts.Agents.Enemies
{
    public abstract class AbstractEnemy : Agent, IWeaponOwner
    {
        [field: SerializeField] public EnemyDataSO EnemyData { get; private set; }

        [Header("무기 관련")]
        [SerializeField] private Transform aimOrigin;
        [SerializeField] private Transform muzzleOrigin;
        [SerializeField] private WeaponData equippedWeaponData;
        [SerializeField] private WeaponHitbox weaponHitbox;

        public INavMesh EnemyNavMeshAgent { get; private set; }
        protected BehaviorGraphAgent BehaviorAgent { get; private set; }

        public IWeapon Weapon { get; private set; }
        public Transform AimOrigin => aimOrigin != null ? aimOrigin : transform;
        public Transform MuzzleOrigin => muzzleOrigin != null ? muzzleOrigin : transform;

        protected override void InitializeModules()
        {
            base.InitializeModules();
            EnemyNavMeshAgent = GetModule<INavMesh>();
            Debug.Assert(EnemyNavMeshAgent != null, $"{gameObject.name}에는 INavMesh모듈이 필요합니다.");
            BehaviorAgent = GetComponent<BehaviorGraphAgent>();
            Debug.Assert(BehaviorAgent != null, $"{gameObject.name}에는 BehaviorGraphAgent가 필요합니다.");
            BehaviorAgent.SetVariableValue("Enemy", this);
            BehaviorAgent.SetVariableValue("AttackCooltime", EnemyData.AttackCooltime);

            if (equippedWeaponData != null)
            {
                Weapon = WeaponFactory.Create(equippedWeaponData);
                Weapon.Equip(this);
                weaponHitbox?.Init(Weapon);
            }
        }

        protected virtual void Update()
        {
            Weapon?.Tick(Time.deltaTime);
        }

        public virtual void Attack()
        {
            Weapon?.PrimaryAttack();
        }

        public void ApplyDamageTo(IDamageable target, float amount, bool isWeakpoint = false)
            => target?.TakeDamage(amount, gameObject);

        public void SetWeaponHitboxActive(bool active)
            => weaponHitbox?.SetActive(active);
    }
}