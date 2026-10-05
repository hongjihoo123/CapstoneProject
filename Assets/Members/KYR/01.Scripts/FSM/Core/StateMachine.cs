using System;
using System.Collections.Generic;

namespace Members.KYR._01_Scripts.FSM.Core
{
    public sealed class StateMachine
    {
        private readonly Dictionary<Type, IState> _states = new();

        public IState Current { get; private set; }

        public Type CurrentType => Current?.GetType();

        public void Register(IState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            _states[state.GetType()] = state;
        }

        public void ChangeState<T>() where T : IState
        {
            Type type = typeof(T);
            if (!_states.TryGetValue(type, out IState next))
                throw new InvalidOperationException($"{type.Name} 상태가 등록되지 않았습니다.");

            if (ReferenceEquals(Current, next))
                return;

            Current?.Exit();
            Current = next;
            Current.Enter();
        }

        public bool IsCurrent<T>() where T : IState
        {
            return Current is T;
        }

        public void Tick(float deltaTime)
        {
            Current?.Tick(deltaTime);
        }
    }
}
