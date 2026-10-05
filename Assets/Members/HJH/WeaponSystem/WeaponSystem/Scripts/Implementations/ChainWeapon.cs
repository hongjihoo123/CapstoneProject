using System.Collections.Generic;
using UnityEngine;

namespace RobotWeapons
{
    public class ChainWeapon : WeaponBase, IAttackFeedbackSource, IComboWeapon
    {
        private static readonly int WeakpointLayer = LayerMask.NameToLayer("Weakpoint");

        private readonly ChainWeaponData data;
        private readonly Dictionary<IDamageable, bool> hitThisSwing = new();

        private int nextStep;
        private int activeStep = -1;
        private float stepTimer;
        private float comboResetTimer;
        private bool queuedNext;

        public override bool PrimaryIsHeld => true;

        public int ComboIndex => activeStep >= 0 ? activeStep : nextStep;
        public int ComboCount => data.steps.Length;

        public ChainWeapon(ChainWeaponData d) : base(d) { data = d; }

        public AttackFeedback DescribeAttack(string animId)
        {
            foreach (ChainWeaponData.ComboStep step in data.steps)
            {
                if (step.animId == animId)
                    return new AttackFeedback { IsFire = true };
            }

            return default;
        }

        public override void Unequip()
        {
            EndStep(resetCombo: true);
            base.Unequip();
        }

        public override void PrimaryAttack()
        {
            if (owner == null || data.steps.Length == 0) return;

            if (activeStep >= 0)
            {
                if (stepTimer >= data.steps[activeStep].inputBufferStart)
                    queuedNext = true;
                return;
            }

            BeginStep(nextStep);
        }

        public override void Tick(float dt)
        {
            if (activeStep >= 0)
            {
                ChainWeaponData.ComboStep step = data.steps[activeStep];
                stepTimer += dt;

                if (stepTimer >= step.hitStart && stepTimer <= step.hitEnd)
                    CheckHits(step);

                if (stepTimer >= step.duration)
                    FinishStep();
            }
            else if (nextStep > 0)
            {
                comboResetTimer -= dt;
                if (comboResetTimer <= 0f)
                    nextStep = 0;
            }
        }

        private void BeginStep(int index)
        {
            ChainWeaponData.ComboStep step = data.steps[index];
            activeStep = index;
            stepTimer = 0f;
            queuedNext = false;
            hitThisSwing.Clear();

            SetMoveMultiplier(step.moveSpeedMultiplier);
            RaiseAttackTriggered(step.animId);
        }

        private void FinishStep()
        {
            bool wasLast = activeStep == data.steps.Length - 1;
            bool chain = queuedNext && !wasLast;
            int following = activeStep + 1;

            EndStep(resetCombo: wasLast);

            if (chain)
                BeginStep(following);
            else if (!wasLast)
            {
                nextStep = following;
                comboResetTimer = data.comboResetDelay;
            }
        }

        private void EndStep(bool resetCombo)
        {
            activeStep = -1;
            queuedNext = false;
            hitThisSwing.Clear();
            SetMoveMultiplier(1f);

            if (resetCombo)
                nextStep = 0;
        }

        private void CheckHits(ChainWeaponData.ComboStep step)
        {
            Transform origin = owner.AimOrigin;
            Vector3 center = origin.position + origin.rotation * step.hitCenter;
            Collider[] colliders = Physics.OverlapBox(center, step.hitSize * 0.5f, origin.rotation, data.hitMask, QueryTriggerInteraction.Collide);

            var found = new Dictionary<IDamageable, bool>();
            foreach (Collider collider in colliders)
            {
                IDamageable target = collider.GetComponentInParent<IDamageable>();
                if (target == null || !target.IsAlive || hitThisSwing.ContainsKey(target))
                    continue;

                bool weakpoint = collider.gameObject.layer == WeakpointLayer;
                found[target] = found.TryGetValue(target, out bool already) && already || weakpoint;
            }

            foreach (KeyValuePair<IDamageable, bool> hit in found)
            {
                hitThisSwing[hit.Key] = hit.Value;

                float damage = (data.baseDamage + bonusDamage) * step.damageMultiplier * DamageMultiplier;
                owner.ApplyDamageTo(hit.Key, damage, hit.Value);
                RaiseDamage(damage);
            }
        }

        private void SetMoveMultiplier(float multiplier)
        {
            if (owner is IRecoilCapable recoilCapable)
                recoilCapable.SetMoveSpeedMultiplier(multiplier);
        }
    }
}
