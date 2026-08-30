namespace Members.KYR._01_Scripts.FSM.Core
{
    public interface IState
    {
        void Enter();
        void Exit();
        void Tick(float deltaTime);
    }
}
