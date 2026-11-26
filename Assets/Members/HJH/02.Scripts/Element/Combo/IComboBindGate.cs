using System.Collections.Generic;
using Members.JJH._02_Scripts.ElementsSystem;

namespace Assets.Members.HJH._02.Scripts.Element
{
    public interface IComboBindGate
    {
        string CostLabel(IReadOnlyList<ElementType> sequence);
        bool CanBind(IReadOnlyList<ElementType> sequence, out string reason);
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
