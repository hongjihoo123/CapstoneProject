using TMPro;
using UnityEngine;

namespace Members.KYR._01.Scripts.UI
{
    public class StatRowUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text valueText;

        public void Set(string label, string value)
        {
            nameText.text = label;
            valueText.text = value;
        }
    }
}
