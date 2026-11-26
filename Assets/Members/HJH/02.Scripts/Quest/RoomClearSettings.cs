using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Quest
{
    public class RoomClearSettings : MonoBehaviour
    {
        [SerializeField, Min(0), Tooltip("0 = 자동. 값이 있으면 퀘스트의 '전체 적의 수'로 그대로 씀")]
        private int expectedTotal;

        public int ExpectedTotal => expectedTotal;
    }
}
