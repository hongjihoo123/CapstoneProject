using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module
{
    // Cooldowns are charge based: a cast spends one charge, and charges come back one at a time,
    // each taking the cooldown. The first recharge starts when the skill ends (MaxCharges 1 = classic cooldown).
    public abstract class SkillStateBase : ISkillState
    {
        protected readonly ISkillHost Host;
        protected float EnterTime;
        private int _spentCharges;
        private bool _recharging;
        private float _rechargeEndTime;

        protected SkillStateBase(ISkillHost host) => Host = host;

        public virtual bool AllowsMove => true;
        public virtual bool AllowsFire => true;
        public virtual bool AllowsReload => false;
        public virtual float MoveSpeedMultiplier => 1f;
        public virtual bool IsFinished => true;
        public virtual int MaxCharges => 1;
        public abstract float Cooldown { get; }
        public float Elapsed => Time.time - EnterTime;
        public float CooldownDuration => EffectiveCooldown;

        public int Charges
        {
            get
            {
                Refresh();
                return Mathf.Max(0, MaxCharges - _spentCharges);
            }
        }

        public bool IsReady => Charges > 0;

        // Time until the next charge comes back (0 when every charge is available).
        public float CooldownRemaining
        {
            get
            {
                Refresh();
                return _recharging ? Mathf.Max(0f, _rechargeEndTime - Time.time) : 0f;
            }
        }

        protected float EffectiveCooldown
        {
            get
            {
                float cooldown = Cooldown;
                return cooldown <= 0f ? 0f : cooldown * (1f - Host.CooldownReduction);
            }
        }

        public void ReduceCooldown(float seconds)
        {
            if (!_recharging || seconds <= 0f)
                return;

            _rechargeEndTime -= seconds;
            Refresh();
        }

        public virtual void Enter()
        {
            Refresh();
            _spentCharges = Mathf.Min(MaxCharges, _spentCharges + 1);
            EnterTime = Time.time;
            Host.OnSkillEntered();
        }

        public virtual void Exit()
        {
            if (_spentCharges > 0 && !_recharging)
            {
                _recharging = true;
                _rechargeEndTime = Time.time + EffectiveCooldown;
            }
        }

        public virtual void Tick(float deltaTime) { }
        public virtual void OnAnimationHitEvent() { }

        private void Refresh()
        {
            while (_recharging && Time.time >= _rechargeEndTime)
            {
                _spentCharges--;
                if (_spentCharges > 0)
                    _rechargeEndTime += EffectiveCooldown;
                else
                    _recharging = false;
            }
        }
    }
}
