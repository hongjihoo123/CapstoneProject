using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Tutorial
{
    [CreateAssetMenu(menuName = "HJH/Tutorial/Step - Info", fileName = "Step_Info")]
    public class InfoStep : TutorialStep
    {
        public override bool IsDone(TutorialContext ctx) => true;
    }
}
