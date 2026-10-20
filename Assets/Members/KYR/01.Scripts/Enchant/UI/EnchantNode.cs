using Members.KYR._01.Scripts.Enchant.So;
using UnityEngine;
using UnityEngine.UI;

namespace Members.KYR._01.Scripts.Enchant.UI
{
    public class EnchantNode : MonoBehaviour
    {
        [SerializeField] private EnchantFragment fragment;

        public EnchantFragment Fragment => fragment;

        public void SetColor(Color color)
        {
            Image image = GetComponent<Image>();
            if (image == null)
                return;

            image.color = color;
        }
    }
}
