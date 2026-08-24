using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RobotWeapons.Sample
{
    // 참고용 샘플. IWeaponOwner 구현 + WeaponFactory 사용법 예시.
    public class SampleWeaponOwner : MonoBehaviour, IWeaponOwner, IHealable
    {
        [SerializeField] private Transform aimOrigin;
        [SerializeField] private Transform muzzleOrigin;
        [SerializeField] private WeaponHitbox weaponHitbox;
        [SerializeField] private WeaponData equippedWeaponData;
        [SerializeField] private LaserBeamVisual laserBeamVisual;
        [SerializeField] private HealScreenEffect healScreenEffect;
        [SerializeField] private Slider laserGaugeSlider;
        [SerializeField] private Slider bowChargeSlider;
        [SerializeField] private Text ammoText;
        [SerializeField] private HitFeedback hitFeedback;
        [SerializeField] private MuzzleFlash muzzleFlash;
        [SerializeField] private CinemachineImpulseSource impulseSource;
        [SerializeField] private float gunShakeForce = 0.3f;
        [SerializeField] private float energyBallShakeForce = 1f;
        [SerializeField] private float recoilRecoverySpeed = 8f;
        [SerializeField] private float maxRecoilPitch = 28f; // 현실 고증: 이 각도 이상은 안 넘어감
        [SerializeField] private float dutchSpringStrength = 400f;
        [SerializeField] private float dutchDamping = 4f;
        [SerializeField] private CinemachineCamera cinemachineCamera;
        [SerializeField] private float swapModeCooldown = 0.25f;
        [SerializeField] private TracerVisual tracerVisual;
        [SerializeField] private GameObject scopeOverlay;
        [SerializeField] private Transform weaponModelTransform;
        [SerializeField] private Vector3 adsWeaponLocalPosition = new Vector3(0f, -0.05f, 0.5f);

        private Vector3 hipWeaponLocalPosition;

        private float swapModeCooldownTimer;
        private float currentFOV;

        private Quaternion aimOriginBaseRotation;
        private float recoilPitch;
        private float recoilYaw;
        private float dutch;
        private float dutchVelocity;

        private IWeapon myWeapon;
        private float currentHP = 100f;
        private float maxHP = 100f;
        private float baseMoveSpeed = 5f;
        private float moveSpeed;

        public Transform AimOrigin => aimOrigin;
        public Transform MuzzleOrigin => muzzleOrigin;

        private void Start()
        {
            moveSpeed = baseMoveSpeed;
            aimOriginBaseRotation = aimOrigin.localRotation;
            if (cinemachineCamera != null) currentFOV = cinemachineCamera.Lens.FieldOfView;
            if (weaponModelTransform != null) hipWeaponLocalPosition = weaponModelTransform.localPosition;
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

            bool fire1Down = GetFire1Down();
            bool fire1Held = GetFire1Held();
            bool fire1Up = GetFire1Up();

            if (fire1Down) myWeapon.OnPrimaryPressed();
            if (fire1Held) myWeapon.OnPrimaryHeld(Time.deltaTime);
            if (fire1Up) myWeapon.OnPrimaryReleased();

            bool fire1 = myWeapon.PrimaryIsHeld ? fire1Held : fire1Down;
            if (fire1) myWeapon.PrimaryAttack();
            if (GetFire2Down()) myWeapon.SecondaryAction();
            if (GetReloadDown()) myWeapon.Reload();

            if (swapModeCooldownTimer > 0f) swapModeCooldownTimer -= Time.deltaTime;
            if (swapModeCooldownTimer <= 0f && GetWheelMoved())
            {
                myWeapon.SwapMode();
                swapModeCooldownTimer = swapModeCooldown;
            }

            myWeapon.Tick(Time.deltaTime);

            // UpdateRecoilRecovery(); // 이전: 발사 여부 상관없이 항상 감쇠
            UpdateRecoilRecovery(fire1); // 지금 쏘고 있는 중인지 넘겨서, 쏘는 동안은 복구 안 되게

            UpdateZoom();
            UpdateBowDraw();
            UpdateAmmoUI();

            if (myWeapon is LaserDealerWeapon laser)
            {
                laserBeamVisual?.UpdateBeam(laser.IsFiring, muzzleOrigin.position, laser.BeamEndPoint);
                if (laserGaugeSlider != null) laserGaugeSlider.value = laser.GaugeRatio;
            }
            else
            {
                laserBeamVisual?.UpdateBeam(false, Vector3.zero, Vector3.zero);
            }
        }

        private bool GetFire1Up()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame;
#else
            return Input.GetButtonUp("Fire1");
#endif
        }

        private bool GetWheelMoved()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null && Mathf.Abs(Mouse.current.scroll.ReadValue().y) > 0.01f;
#else
            return Mathf.Abs(Input.GetAxis("Mouse ScrollWheel")) > 0.01f;
#endif
        }

        private bool GetReloadDown()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.R);
#endif
        }

        private void UpdateBowDraw()
        {
            if (myWeapon is not BowWeapon bow)
            {
                if (bowChargeSlider != null) bowChargeSlider.value = 0f;
                return;
            }

            if (bowChargeSlider != null) bowChargeSlider.value = bow.ChargeRatio;

            if (weaponModelTransform == null) return;

            if (!bow.IsFullyDrawn)
            {
                weaponModelTransform.localPosition = hipWeaponLocalPosition;
                return;
            }

            float t = Time.time * bow.FullDrawShakeSpeed;
            Vector3 shake = new Vector3(
                (Mathf.PerlinNoise(t, 0f) - 0.5f) * bow.FullDrawShakeAmount,
                (Mathf.PerlinNoise(0f, t) - 0.5f) * bow.FullDrawShakeAmount,
                0f);
            weaponModelTransform.localPosition = hipWeaponLocalPosition + shake;
        }

        private void UpdateZoom()
        {
            if (myWeapon is SniperSawedOffWeapon sniper)
            {
                if (cinemachineCamera != null)
                {
                    float targetFOV = sniper.IsZoomed ? sniper.ZoomFOV : sniper.DefaultFOV;
                    currentFOV = Mathf.Lerp(currentFOV, targetFOV, Time.deltaTime * sniper.ZoomTransitionSpeed);
                    cinemachineCamera.Lens.FieldOfView = currentFOV;

                    if (sniper.IsZoomed)
                    {
                        float t = Time.time * sniper.ZoomSwaySpeed;
                        float swayX = (Mathf.PerlinNoise(t, 0f) - 0.5f) * 2f * sniper.ZoomSwayAmount;
                        float swayY = (Mathf.PerlinNoise(0f, t) - 0.5f) * 2f * sniper.ZoomSwayAmount;
                        aimOrigin.localRotation *= Quaternion.Euler(swayY, swayX, 0f);
                    }
                }

                scopeOverlay?.SetActive(sniper.IsZoomed);

                // 줌하면 무기가 화면 가운데(가슴팍)로, 풀면 원래 자리로. 토글식(보간 없이 즉시 전환).
                if (weaponModelTransform != null)
                    weaponModelTransform.localPosition = sniper.IsZoomed ? adsWeaponLocalPosition : hipWeaponLocalPosition;
            }
            else
            {
                if(scopeOverlay != null) scopeOverlay?.SetActive(false);
            }
        }

        private void UpdateAmmoUI()
        {
            if (ammoText == null) return;

            if (myWeapon is MeleeSawedOffWeapon melee)
            {
                ammoText.gameObject.SetActive(melee.IsShotgunMode);
                if (melee.IsShotgunMode)
                    ammoText.text = melee.ShotgunIsReloading ? "재장전 중..." : $"{melee.ShotgunCurrentAmmo} / {melee.ShotgunMaxAmmo}";
                return;
            }

            if (myWeapon is SniperSawedOffWeapon sniper)
            {
                ammoText.gameObject.SetActive(true);
                if (sniper.IsShotgunMode)
                    ammoText.text = sniper.ShotgunIsReloading ? "재장전 중..." : $"{sniper.ShotgunCurrentAmmo} / {sniper.ShotgunMaxAmmo}";
                else
                    ammoText.text = sniper.SniperIsReloading ? "재장전 중..." : $"{sniper.SniperCurrentAmmo} / {sniper.SniperMaxAmmo}";
                return;
            }

            bool hasAmmo = myWeapon is GunDealerWeapon || myWeapon is LaserDealerWeapon;
            ammoText.gameObject.SetActive(hasAmmo);
            if (!hasAmmo) return;

            ammoText.text = myWeapon.IsReloading
                ? "재장전 중..."
                : $"{Mathf.CeilToInt(myWeapon.CurrentResource)} / {Mathf.CeilToInt(myWeapon.MaxResource)}";
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

            switch (animId)
            {
                case "Gun_Fire":
                    muzzleFlash?.Play();
                    impulseSource?.GenerateImpulseWithForce(gunShakeForce);
                    break;
                case "Laser_EnergyBall":
                    float chargeRatio = (myWeapon as LaserDealerWeapon)?.LastFireChargeRatio ?? 1f;
                    impulseSource?.GenerateImpulseWithForce(energyBallShakeForce * chargeRatio);
                    break;
                case "Sniper_Fire":
                    if (myWeapon is SniperSawedOffWeapon sniperShot)
                        tracerVisual?.Fire(sniperShot.LastShotStart, sniperShot.LastShotEnd);
                    impulseSource?.GenerateImpulseWithForce(gunShakeForce);
                    break;
                case "SawedOff_FireLeft":
                case "SawedOff_FireRight":
                    muzzleFlash?.Play();
                    impulseSource?.GenerateImpulseWithForce(gunShakeForce);
                    break;
            }

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

        public void ApplyDamageTo(IDamageable target, float amount, bool isWeakpoint = false)
        {
            target?.TakeDamage(amount, gameObject);
            hitFeedback?.Play(isWeakpoint);
        }
        public void ApplyHealTo(IHealable target, float amount) => target?.Heal(amount);
        public bool IsAlive => currentHP > 0f;

        public void Heal(float amount)
        {
            currentHP = Mathf.Min(maxHP, currentHP + amount);
            healScreenEffect?.Pulse();
        }
        public void SetMoveSpeedMultiplier(float multiplier) => moveSpeed = baseMoveSpeed * multiplier;

        public void ApplyRecoil(float pitchDelta, float yawDelta, float dutchImpulse = 0f)
        {
            recoilPitch += pitchDelta;
            recoilYaw += yawDelta;
            dutchVelocity += dutchImpulse;
        }

        private void UpdateRecoilRecovery(bool isFiring)
        {
            // --- 이전 버전: 매 프레임 무조건 감쇠라, 발사 중에도 상승분과 감쇠분이
            //     평형점을 이루면서 일정 각도에서 더 안 올라가는 문제가 있었음 ---
            // recoilPitch = Mathf.Lerp(recoilPitch, 0f, Time.deltaTime * recoilRecoverySpeed);
            // recoilYaw = Mathf.Lerp(recoilYaw, 0f, Time.deltaTime * recoilRecoverySpeed);

            // 발사 중엔 복구 안 하고 계속 누적, 손 뗐을 때(!isFiring)만 복구 시작
            if (!isFiring)
            {
                recoilPitch = Mathf.Lerp(recoilPitch, 0f, Time.deltaTime * recoilRecoverySpeed);
                recoilYaw = Mathf.Lerp(recoilYaw, 0f, Time.deltaTime * recoilRecoverySpeed);
            }

            // 현실 고증: 계속 쏴도 이 각도 이상 안 넘어가게 (총이 뒤로 완전히 넘어가진 않음)
            recoilPitch = Mathf.Clamp(recoilPitch, 0f, maxRecoilPitch);

            dutchVelocity += -dutch * dutchSpringStrength * Time.deltaTime;
            dutchVelocity *= Mathf.Clamp01(1f - dutchDamping * Time.deltaTime);
            dutch += dutchVelocity * Time.deltaTime;

            aimOrigin.localRotation = aimOriginBaseRotation * Quaternion.Euler(-recoilPitch, recoilYaw, dutch);
        }
        public void SetWeaponHitboxActive(bool active) => weaponHitbox?.SetActive(active);
    }
}
