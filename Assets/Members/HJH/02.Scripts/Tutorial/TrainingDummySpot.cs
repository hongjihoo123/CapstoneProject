using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Tutorial
{
    // 마무리 훈련(TargetWaveStep)에서 더미가 설 자리. 맵에 빈 오브젝트로 놓고 옮기면 됨.
    public class TrainingDummySpot : MonoBehaviour
    {
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.75f, 0.3f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.5f, 0.5f);
        }
    }
}
