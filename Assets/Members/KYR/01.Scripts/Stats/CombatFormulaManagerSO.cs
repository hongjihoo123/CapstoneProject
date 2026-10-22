using UnityEngine;

namespace Members.KYR._01_Scripts.Stats
{
    [CreateAssetMenu(fileName = "CombatFormula", menuName = "Stats/Combat Formula")]
    public class CombatFormulaManagerSO : ScriptableObject
    {
        [SerializeField, Min(0.01f), Tooltip("클수록 같은 방어로 덜 막음. 100이면 방어 100 = 50%.")]
        private float defenseK = 100f;

        [SerializeField, Min(0.01f), Tooltip("속성 저항 점감. 비우면 방어 K와 같음.")]
        private float elementK = 100f;

        public float DefenseK => Mathf.Max(0.01f, defenseK);
        public float ElementK => Mathf.Max(0.01f, elementK);
    }
}
