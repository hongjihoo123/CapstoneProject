using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Quest
{
    // 방(씬)마다 "이 방의 적은 총 몇 마리"를 직접 정해 두는 설정. 씬에 빈 오브젝트로 하나 두면 됨.
    // 적이 세트로 나눠서 나오거나(7마리씩 3번) 스포너 방식이 달라서 자동 계산이 안 맞을 때 쓰는 확실한 방법.
    // 0 이면 자동 계산(스포너 웨이브 합 + 씬에 있는 적, 꺼져 있는 적 포함)
    public class RoomClearSettings : MonoBehaviour
    {
        [SerializeField, Min(0), Tooltip("0 = 자동. 값이 있으면 퀘스트의 '전체 적의 수'로 그대로 씀")]
        private int expectedTotal;

        public int ExpectedTotal => expectedTotal;
    }
}
