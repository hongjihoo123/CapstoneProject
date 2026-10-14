namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module
{
    // A running multi-frame skill (dash strike, charge beam, ...). Created by SkillData.Begin, ticked every frame
    // with the time since cast, ended when the state exits (finished, cancelled or interrupted).
    public interface ISkillExecution
    {
        void Tick(float elapsed);
        void OnAnimationEvent(SkillAnimationEvent animationEvent);
        void End();
    }
}
