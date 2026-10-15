using Members.KYR._01.Scripts.Enchant.Data;
using UnityEngine;

namespace Members.KYR._01.Scripts.Enchant.So
{
    [CreateAssetMenu(fileName = "EnchantOption", menuName = "SO/Enchant/EnchantOption", order = 50)]
    public class EnchantOption : ScriptableObject
    {
        [SerializeField] private EnchantOptionKind kind = EnchantOptionKind.AttackUp;
        [SerializeField] private string displayName ="공격력 5% 증가";

        public  EnchantOptionKind Kind => kind;
        public string DisplayName => displayName;
    }
}