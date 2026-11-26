using Assets.Members.HJH._02.Scripts.Char.Visual;
using Assets.Members.HJH._02.Scripts.Element.Upgrade;
using RobotWeapons;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.Gunner
{
    [CreateAssetMenu(menuName = "Skill/Passive/Gunner Charged Shot")]
    public class ChargedShotPassiveData : PassiveData
    {
        [Header("게이지")]
        [SerializeField, Min(1), Tooltip("이만큼 쏘면 게이지 꽉 참")] private int shotsToFill = 10;

        [Header("강화 평타 (충전 관통 충격포)")]
        [SerializeField] private ChargedPiercingShot.Settings shot = new()
        {
            range = 16f,
            width = 1.2f,
            windup = 0.12f,
            damage = 45f,
            knockback = 1.5f,
            recoil = 0.7f,
            hitStop = 0.06f,
            chargeRumble = 0.08f,
            fireShake = 0.3f,
            cameraKick = 0.45f,
            color = new Color(0.3f, 1f, 0.85f)
        };

        public override IPassive CreateRuntime(IPassiveHost host) => new Runtime(this, host);

        private sealed class Runtime : PassiveRuntime, IPassiveGauge
        {
            private readonly ChargedShotPassiveData _data;
            private IShotInterceptable _gun;
            private float _shots;
            private FxHandle _glow;

            public Runtime(ChargedShotPassiveData data, IPassiveHost host) : base(host)
            {
                _data = data;
                CritSystem.Landed += OnCrit;
                CritSystem.Cast += OnCast;
            }

            private float Need => Mathf.Max(1f, _data.shotsToFill - SkillUpgrades.Bonus(SkillUpgradeKind.ChargeNeed, SkillUpgradeScope.Charged));

            public float Fill => Mathf.Clamp01(_shots / Need);
            public bool IsFull => _shots >= Need;

            private void OnCrit(SkillUpgradeScope slot)
            {
                if (slot == SkillUpgradeScope.Charged)
                    _shots = Mathf.Min(Need, _shots + SkillUpgrades.Exact(SkillUpgradeKind.CritGauge, SkillUpgradeScope.Charged));
            }

            private void OnCast(Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.SkillSlotId slot)
            {
                if (slot == Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.SkillSlotId.Dash)
                    _shots = Mathf.Min(Need, _shots + SkillUpgrades.Exact(SkillUpgradeKind.DashGauge, SkillUpgradeScope.Charged));
            }

            public override void OnEnemyKilled() =>
                _shots = Mathf.Min(Need, _shots + SkillUpgrades.Exact(SkillUpgradeKind.KillGauge, SkillUpgradeScope.Charged));

            public override void Tick(float deltaTime)
            {
                BindGun();
                UpdateGlow();
            }

            public override void Dispose()
            {
                CritSystem.Landed -= OnCrit;
                CritSystem.Cast -= OnCast;
                if (_gun != null)
                    _gun.ShotInterceptor = null;
                _gun = null;
                _glow?.Kill();
                _glow = null;
            }

            private void BindGun()
            {
                var current = Context.Weapon as IShotInterceptable;
                if (ReferenceEquals(current, _gun))
                    return;

                if (_gun != null)
                    _gun.ShotInterceptor = null;
                _gun = current;
                if (_gun != null)
                    _gun.ShotInterceptor = OnTrigger;
            }

            private bool OnTrigger(Vector3 muzzle, Vector3 direction)
            {
                if (!IsFull)
                {
                    _shots++;
                    return false;
                }

                _shots = 0;
                var muzzleSource = _gun as IMuzzleSource;
                Context.Run(new ChargedPiercingShot(Context, muzzle, direction, _data.shot,
                    muzzleSource != null ? () => muzzleSource.CurrentMuzzle : null,
                    _gun as IAnimationTimedShots));
                if (SkillUpgrades.Exact(SkillUpgradeKind.ChargeDouble, SkillUpgradeScope.Charged) > 0f)
                {
                    var second = _data.shot;
                    second.windup += 0.3f;
                    Context.Run(new ChargedPiercingShot(Context, muzzle, direction, second,
                        muzzleSource != null ? () => muzzleSource.CurrentMuzzle : null));
                }
                return true;
            }

            private void UpdateGlow()
            {
                if (!IsFull)
                {
                    _glow?.Kill();
                    _glow = null;
                    return;
                }

                Vector3 position = _gun is IMuzzleSource muzzle
                    ? muzzle.CurrentMuzzle
                    : Context.Transform.position + Vector3.up + Context.AimDirection * 0.6f;
                _glow ??= Fx.Charge(position, _data.shot.color);
                _glow.MoveTo(position);
                _glow.SetValue(0.35f + 0.15f * Mathf.Sin(Time.time * 10f));
            }
        }
    }
}
