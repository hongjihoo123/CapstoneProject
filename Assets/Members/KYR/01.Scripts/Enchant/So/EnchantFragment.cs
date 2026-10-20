using Members.KYR._01.Scripts.Enchant.Data;
using UnityEngine;

namespace Members.KYR._01.Scripts.Enchant.So
{
    [CreateAssetMenu(fileName = "EnchantFragment", menuName = "SO/Enchant/EnchantFragment", order = 30)]
    public class EnchantFragment : ScriptableObject
    {
        [SerializeField] private string fragmentId;
        [SerializeField] private string displayName;
        [SerializeField] private EnchantBranch branch;
        [SerializeField] private int unlockCost = 123;
        [SerializeField] private EnchantOption[] options = new EnchantOption[3];

        public string FragmentId => fragmentId;
        public string DisplayName => displayName;
        public EnchantBranch Branch => branch;
        public int UnlockCost => unlockCost;
        public EnchantOption[] Options => options;
    }
}