using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Tutorial
{
    // 설명만 하는 단계: 시간을 멈추고 카드를 띄움 → 확인(Enter / 계속 버튼)하면 끝
    [CreateAssetMenu(menuName = "HJH/Tutorial/Step - Info", fileName = "Step_Info")]
    public class InfoStep : TutorialStep
    {
        public override bool IsDone(TutorialContext ctx) => true;
    }
}
