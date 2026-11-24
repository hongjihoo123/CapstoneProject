using Assets.Members.HJH._02.Scripts.Char.Visual;
using Assets.Members.HJH._02.Scripts.Element.Upgrade;
using RobotWeapons;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.Gunner
{
    // 건슬링어 패시브: 총 쏠 때마다 게이지가 참. 꽉 차면 다음 좌클릭이 일반 총알 대신
    // "충전 관통 충격포"(ChargedPiercingShot)로 나가고 게이지는 다시 0부터.
    //  - 게이지 꽉 차 있는 동안 총구에 빛이 맺혀 있음 (총이 알려주는 총구 위치를 매 프레임 따라감)
    //  - 스킬바 패시브 칸에 게이지 차오르는 거 보임 (IPassiveGauge)
    // 총이 "다음 한 발 대신 쏴줄래?" 물어보는 통로(IShotInterceptable)에 끼어들어서 바꿔치기하는 방식임.
    // 총이 쏘기 직전마다 물어보니까 거기서 한 발씩 세면 됨
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

            // 스킬 트리: 충전 필요 발수 감소
            private float Need => Mathf.Max(1f, _data.shotsToFill - SkillUpgrades.Bonus(SkillUpgradeKind.ChargeNeed, SkillUpgradeScope.Charged));

            public float Fill => Mathf.Clamp01(_shots / Need);
            public bool IsFull => _shots >= Need;

            // 스킬 트리: 충전탄이 치명이면 게이지 +, 처치하면 게이지 +
            private void OnCrit(SkillUpgradeScope slot)
            {
                if (slot == SkillUpgradeScope.Charged)
                    _shots = Mathf.Min(Need, _shots + SkillUpgrades.Exact(SkillUpgradeKind.CritGauge, SkillUpgradeScope.Charged));
            }

            // 대쉬 시 게이지 (잔류 충전)
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

            // 총은 캐릭터/무기 바뀔 때마다 새로 만들어져서 매 프레임 확인함
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

            // 총이 쏘기 직전마다 불림.
            // 게이지 꽉 찼으면 충격포 쏘고 "내가 쐈음(true)", 아니면 게이지 한 칸 채우고 일반 총알 그대로(false)
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
                    _gun as IAnimationTimedShots));   // 애니 이벤트로 쏘는 총이면 PassiveShot 클립의 Anim_Fire 프레임에 발사
                // 과충전: 충전탄 2연발 (두 번째는 조금 늦게, 시간으로 충전)
                if (SkillUpgrades.Exact(SkillUpgradeKind.ChargeDouble, SkillUpgradeScope.Charged) > 0f)
                {
                    var second = _data.shot;
                    second.windup += 0.3f;
                    Context.Run(new ChargedPiercingShot(Context, muzzle, direction, second,
                        muzzleSource != null ? () => muzzleSource.CurrentMuzzle : null));
                }
                return true;
            }

            // 게이지 꽉 차 있는 동안 총구에 빛 맺혀 있음
            private void UpdateGlow()
            {
                if (!IsFull)
                {
                    _glow?.Kill();
                    _glow = null;
                    return;
                }

                // 총구 위치에 빛 맺힘 (총이 위치를 못 알려주면 플레이어 앞쪽)
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
