using System.Collections.Generic;
using Members.JJH._02_Scripts.ElementsSystem;

namespace Assets.Members.HJH._02.Scripts.Element
{
    // 바인딩을 해도 되는지 + 대가를 치르는 곳. 지금은 FreeBindGate (항상 무료).
    // 나중에 재화(골드)나 레벨 조건이 생기면 이걸 구현해서 ComboLoadout.Gate 에 넣으면 됨
    public interface IComboBindGate
    {
        // 바인딩 창 아래에 보여줄 비용 글자 (예: "무료", "골드 120")
        string CostLabel(IReadOnlyList<ElementType> sequence);
        bool CanBind(IReadOnlyList<ElementType> sequence, out string reason);
        // 저장이 확정된 뒤 한 번 (재화 차감 등)
        void OnBound(IReadOnlyList<ElementType> sequence);
    }

    public sealed class FreeBindGate : IComboBindGate
    {
        public string CostLabel(IReadOnlyList<ElementType> sequence) => "무료 (임시)";

        public bool CanBind(IReadOnlyList<ElementType> sequence, out string reason)
        {
            reason = null;
            return true;
        }

        public void OnBound(IReadOnlyList<ElementType> sequence) { }
    }
}
