using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RobotWeapons.Sample
{
    // 참고용 샘플. IWeaponOwner 구현 + WeaponFactory 사용법 예시.
    public class SampleWeaponOwner : MonoBehaviour, IWeaponOwner
    {
        [SerializeField] private Transform attackOrigin;
        [SerializeField] private WeaponHitbox weaponHitbox;
        [SerializeField] private WeaponData equippedWeaponData;
        [SerializeField] private LaserBeamVisual laserBeamVisual;

        private IWeapon myWeapon;
        private float currentHP = 100f;
        private float maxHP = 100f;
        private float baseMoveSpeed = 5f;
        private float moveSpeed;

        public Transform AttackOrigin => attackOrigin;

        private void Start()
        {
            moveSpeed = baseMoveSpeed;
            EquipWeapon(WeaponFactory.Create(equippedWeaponData));
        }

        public void EquipWeapon(IWeapon weapon)
        {
            myWeapon?.Unequip();
            myWeapon = weapon;
            myWeapon.Equip(this);
            weaponHitbox?.Init(myWeapon);
            myWeapon.OnAttackTriggered += HandleAttackTriggered;
        }

        private void Update()
        {
            if (myWeapon == null) return;

            bool fire1 = myWeapon.PrimaryIsHeld ? GetFire1Held() : GetFire1Down();
            if (fire1) myWeapon.PrimaryAttack();
            if (GetFire2Down()) myWeapon.SecondaryAction();

            myWeapon.Tick(Time.deltaTime);

            if (myWeapon is LaserDealerWeapon laser)
                laserBeamVisual?.UpdateBeam(laser.IsFiring, attackOrigin.position, laser.BeamEndPoint);
            else
                laserBeamVisual?.UpdateBeam(false, Vector3.zero, Vector3.zero);
        }

        private bool GetFire1Held()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null && Mouse.current.leftButton.isPressed;
#else
            return Input.GetButton("Fire1");
#endif
        }

        // 좌클릭=Fire1, 우클릭=Fire2. 새/구 Input System 자동 분기.
        private bool GetFire1Down()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
            return Input.GetButtonDown("Fire1");
#endif
        }

        private bool GetFire2Down()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
#else
            return Input.GetButtonDown("Fire2");
#endif
        }

        [Header("디버그 (애니메이터 연결 전 임시 테스트용, 실제 연결 시 꺼둘 것)")]
        [SerializeField] private bool useDebugTimingSimulation = true;
        [SerializeField] private float debugSwingDuration = 0.25f;
        [SerializeField] private float debugHitDelay = 0.15f;

        private void HandleAttackTriggered(string animId)
        {
            Debug.Log($"애니메이션 트리거: {animId}");

            if (!useDebugTimingSimulation) return;

            if (animId.Contains("MeleeSwing") || animId.StartsWith("SubDealer_Attack"))
                StartCoroutine(SimulateMeleeSwing());
        }

        private IEnumerator SimulateMeleeSwing()
        {
            myWeapon.StartHitWindow();
            yield return new WaitForSeconds(debugSwingDuration);
            myWeapon.EndHitWindow();
        }

        private IEnumerator SimulateSingleHit()
        {
            yield return new WaitForSeconds(debugHitDelay);
            myWeapon.ExecuteHit();
        }

        public void Anim_ExecuteHit() => myWeapon?.ExecuteHit();
        public void Anim_SwingStart() => myWeapon?.StartHitWindow();
        public void Anim_SwingEnd() => myWeapon?.EndHitWindow();

        public void ApplyDamageTo(IDamageable target, float amount) => target?.TakeDamage(amount, gameObject);
        public void ApplyHealTo(IHealable target, float amount) => target?.Heal(amount);
        public void Heal(float amount) => currentHP = Mathf.Min(maxHP, currentHP + amount);
        public void SetMoveSpeedMultiplier(float multiplier) => moveSpeed = baseMoveSpeed * multiplier;
        public void SetWeaponHitboxActive(bool active) => weaponHitbox?.SetActive(active);
    }
}
