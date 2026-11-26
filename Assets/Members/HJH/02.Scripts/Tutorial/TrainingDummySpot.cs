using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Tutorial
{
    public class TrainingDummySpot : MonoBehaviour
    {
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.75f, 0.3f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.5f, 0.5f);
        }
    }
}
