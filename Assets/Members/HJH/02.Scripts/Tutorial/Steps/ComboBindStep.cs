using Assets.Members.HJH._02.Scripts.Element;
using Assets.Members.HJH._02.Scripts.UI;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Tutorial
{
    [CreateAssetMenu(menuName = "HJH/Tutorial/Step - Combo Bind", fileName = "Step_ComboBind")]
    public class ComboBindStep : TutorialStep
    {
        [System.NonSerialized] private ComboBindStation _station;
        [System.NonSerialized] private string _boundAtStart;

        public override bool ShouldRun(TutorialContext ctx)
        {
            _station = Object.FindFirstObjectByType<ComboBindStation>();
            return _station != null;
        }

        public override void Begin(TutorialContext ctx)
        {
            _boundAtStart = ComboLoadout.NameOf(ComboLoadout.Bound);
        }

        public override bool IsDone(TutorialContext ctx)
        {
            bool open = ComboBindPanel.IsOpen;
            ctx.View.SetGuidePoint(_station != null && !open ? _station.transform.position : (Vector3?)null);
            return !open && ComboLoadout.NameOf(ComboLoadout.Bound) != _boundAtStart;
        }

        public override string Progress(TutorialContext ctx)
        {
            if (ComboBindPanel.IsOpen)
                return "원소를 2~4칸 채우고 " + TutorialContext.Key("저장하기");
            if (_station != null && _station.PlayerNear)
                return TutorialContext.Key("F") + " 콤보 바인딩";
            return "화살표 쪽 콤보 상점으로 가세요";
        }

        public override void End(TutorialContext ctx) => ctx.View.SetGuidePoint(null);
    }
}
