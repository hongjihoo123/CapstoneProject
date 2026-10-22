using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.ElementSkill
{
    // 맞은 대상이 "이펙트는 여기에 띄워줘" 하고 다른 몸을 알려줄 때 (선택).
    // 보통 적은 구현 안 함 → 자기 몸에 맞은 이펙트·CC 표시가 뜸.
    // FxWorkbench 의 허수아비는 판정은 스킬 쪽에서 받고, 이펙트는 따로 세워둔 몸에 띄우려고 씀.
    public interface IFxBody
    {
        Transform FxBody { get; }
    }
}
