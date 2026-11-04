using UnityEngine;

namespace Members.KYR._01.Scripts.Stats
{
    [CreateAssetMenu(fileName = "CombatFormula", menuName = "Stats/Combat Formula")]
    public class CombatFormulaManagerSO : ScriptableObject
    {
        [SerializeField, Min(0.01f), Tooltip("방어점감비율")]
        private float defenseK = 100f;

        [SerializeField, Min(0.01f), Tooltip("속성 저항 점감 비율")]
        private float elementK = 100f;

        public float DefenseK => Mathf.Max(0.01f, defenseK);
        public float ElementK => Mathf.Max(0.01f, elementK);
    }
}
