using System.Collections.Generic;
using Members.KYR._01_Scripts;
using Members.KYR._01.Scripts.Player;

namespace Members.KYR._01_Scripts.EffectBuffs
{
    public sealed class EffectBuffRunner
    {
        private sealed class Active
        {
            public EffectBuff Buff;
            public float Remaining;
            public object State;
        }

        public static EffectBuffRunner Player { get; private set; }

        private readonly EffectBuffContext _context;
        private readonly List<Active> _active = new();

        public EffectBuffRunner(PlayerAgent player)
        {
            _context = new EffectBuffContext(player);
        }

        public void ClaimPlayer()
        {
            Player = this;
        }

        public void ReleasePlayer()
        {
            Clear();
            if (Player == this)
                Player = null;
        }

        public void Apply(EffectBuff buff)
        {
            if (buff == null || buff.Duration <= 0f)
                return;

            for (int i = 0; i < _active.Count; i++)
            {
                if (_active[i].Buff != buff)
                    continue;

                _active[i].Remaining = buff.Duration;
                return;
            }

            object state = buff.CreateState();
            _active.Add(new Active { Buff = buff, Remaining = buff.Duration, State = state });
            buff.OnStart(_context, state);
        }

        public bool Remove(EffectBuff buff)
        {
            if (buff == null)
                return false;

            for (int i = 0; i < _active.Count; i++)
            {
                if (_active[i].Buff != buff)
                    continue;

                object state = _active[i].State;
                _active.RemoveAt(i);
                buff.OnEnd(_context, state);
                return true;
            }

            return false;
        }

        public void Clear()
        {
            if (_active.Count == 0)
                return;

            Active[] ending = _active.ToArray();
            _active.Clear();
            for (int i = ending.Length - 1; i >= 0; i--)
                ending[i].Buff.OnEnd(_context, ending[i].State);
        }

        public void Tick(float deltaTime)
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                Active active = _active[i];
                active.Buff.Tick(_context, active.State, deltaTime);
                active.Remaining -= deltaTime;
                if (active.Remaining > 0f)
                    continue;

                _active.RemoveAt(i);
                active.Buff.OnEnd(_context, active.State);
            }
        }

        public void NotifyBasicAttack()
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                if (i >= _active.Count)
                    continue;

                _active[i].Buff.OnBasicAttack(_context, _active[i].State);
            }
        }
    }
}
