using UnityEngine;

namespace MemberWorkSpace.JJH._02_Scripts.Systems.AnimatorSystem
{
    [CreateAssetMenu(fileName = "Hash data", menuName = "SO/Animator Hash data", order = 1)]
    public class AnimParamSO : ScriptableObject
    {
        [field: SerializeField] public string HashName { get; private set; }
        [field: SerializeField] public int HashValue { get; private set; }

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(HashName))
            {
                HashValue = 0;
                return;
            }

            HashValue = Animator.StringToHash(HashName);
        }
    }
}