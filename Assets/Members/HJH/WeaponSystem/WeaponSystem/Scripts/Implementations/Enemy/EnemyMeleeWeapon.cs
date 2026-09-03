using Assets.Members.HJH.WeaponSystem.WeaponSystem.Scripts.Data.Enemy;
using RobotWeapons;
using System.Collections.Generic;
using static UnityEngine.UI.GridLayoutGroup;

public class EnemyMeleeWeapon : WeaponBase
{
    private readonly EnemyMeleeData data;
    private float cooldown;
    private readonly HashSet<IDamageable> hitThisSwing = new();
    private bool hitWindowActive;

    public EnemyMeleeWeapon(EnemyMeleeData d) : base(d) { data = d; }

    public override void Tick(float dt) => cooldown -= dt;

    public override void PrimaryAttack()
    {
        if (cooldown > 0f) return;
        cooldown = data.attackCooldown;
        RaiseAttackTriggered("Melee_Swing"); // 여기에 Anim ID만 바꿔주면 됨. 지금은 임시로 써둠
    }

    public override void StartHitWindow()
    {
        hitThisSwing.Clear();
        hitWindowActive = true;
        owner?.SetWeaponHitboxActive(true);
    }

    public override void EndHitWindow()
    {
        hitWindowActive = false;
        owner?.SetWeaponHitboxActive(false);
    }

    public override void OnHitboxTouch(IDamageable target, bool isWeakpoint)
    {
        if (!hitWindowActive || target == null || !target.IsAlive || hitThisSwing.Contains(target))
            return;
        hitThisSwing.Add(target);

        float dmg = data.meleeDamage * DamageMultiplier;
        owner.ApplyDamageTo(target, dmg, isWeakpoint);
        RaiseDamage(dmg);
    }
}