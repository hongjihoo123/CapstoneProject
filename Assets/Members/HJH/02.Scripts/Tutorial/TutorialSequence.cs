using System.Collections.Generic;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Tutorial
{
    [CreateAssetMenu(menuName = "HJH/Tutorial/Sequence", fileName = "Tutorial_Sequence")]
    public class TutorialSequence : ScriptableObject
    {
        [SerializeField] private List<TutorialStep> steps = new();

        [Tooltip("마지막 단계가 끝나면 튜토리얼 전체를 마친 것으로 저장하고 이 씬으로 감 (비우면 그대로 둠)")]
        [SerializeField] private string sceneWhenFinished = "";

        [Tooltip("카드를 화면 아래쪽에 (위쪽을 가리면 안 되는 화면용, 예: 픽 화면의 캐릭터 얼굴)")]
        [SerializeField] private bool cardAtBottom;
        [SerializeField] private float cardOffset = 120f;

        public IReadOnlyList<TutorialStep> Steps => steps;
        public bool CardAtBottom => cardAtBottom;
        public float CardOffset => cardOffset;
        public string SceneWhenFinished => sceneWhenFinished;

        public static TutorialSequence Create(string sceneWhenFinished, bool cardAtBottom, params TutorialStep[] steps)
        {
            var sequence = CreateInstance<TutorialSequence>();
            sequence.cardAtBottom = cardAtBottom;
            sequence.steps = new List<TutorialStep>(steps);
            sequence.sceneWhenFinished = sceneWhenFinished;
            return sequence;
        }
    }
}
