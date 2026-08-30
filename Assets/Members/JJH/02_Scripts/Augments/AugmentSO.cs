using UnityEngine;

namespace Members.JJH._02_Scripts.Augments
{
    [CreateAssetMenu(fileName = "Augment Data", menuName = "SO/Augment")]
    public class AugmentSO : ScriptableObject
    {
        [Header("Augment Setting")]
        [field: SerializeField] public string AugmentName { get; private set; }
        [field: SerializeField] public string AugmentDescription { get; private set; }
        [field: SerializeField] public Sprite AugmentIcon { get; private set; }
    }
}