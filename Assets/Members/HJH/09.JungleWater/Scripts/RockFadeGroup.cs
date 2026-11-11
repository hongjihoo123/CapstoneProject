using UnityEngine;

namespace Assets.Members.HJH.Environment
{
    // 이 오브젝트 아래의 바위 렌더러들을 한 묶음으로 본다: 묶음 안의 바위 하나라도 카메라를 가려 투명해지면 나머지도 함께 투명해진다.
    // (이름이 "RockMass_" 로 시작하는 오브젝트는 이 컴포넌트 없이도 자동으로 묶음 취급 — JungleOcclusionFader.FindGroupRoot)
    [DisallowMultipleComponent]
    public class RockFadeGroup : MonoBehaviour { }
}
