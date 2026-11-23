using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Tutorial
{
    // 튜토리얼 한 단계. 에셋 하나 = 단계 하나, TutorialSequence 에 순서대로 넣으면 됨.
    // 새 단계 종류가 필요하면 이 클래스를 상속해서 IsDone 만 채우면 됨 (나머지는 선택).
    //
    // 한 단계가 도는 순서 (TutorialDirector):
    //   ShouldRun → Begin(적 소환 등 준비) → 카드 표시
    //   → pauseTime 이면 시간 정지, IsResumeInput 이 들어오면 다시 흐름
    //   → 매 프레임 IsDone 확인 (그동안 화면 위쪽에 작은 목표 카드 + Progress)
    //   → End(정리) → 다음 단계.   Tab = 이 단계 건너뛰기 (End 는 그래도 불림)
    //
    // 제목/설명에는 키 이름 자리표시를 쓸 수 있음: {Fire} {Weapon} {Dash} {Ultimate} {Basic1} {Basic2} {Finisher} {Confirm}
    // 에셋은 여러 번 재사용되니 실행 중 값은 [System.NonSerialized] 필드에 두고 Begin 에서 초기화할 것.
    public abstract class TutorialStep : ScriptableObject
    {
        [SerializeField] private string title = "";
        [SerializeField, TextArea(2, 6)] private string body = "";
        [SerializeField, Tooltip("테두리로 강조할 UI (TutorialAnchorIds). 비우면 강조 없음")]
        private string anchorId = "";
        [SerializeField, Tooltip("카드를 띄우는 동안 시간(Time.timeScale)을 멈춤")]
        private bool pauseTime = true;
        [SerializeField, Tooltip("카드 아래 안내. 비우면 단계 기본값")]
        private string resumeHint = "";

        public string Title => title;
        public string Body => body;
        public virtual string AnchorId => anchorId;
        public bool PauseTime => pauseTime;

        // 카드에 나오는 글 (키 이름 채움). 상황에 따라 글을 덧붙이는 단계는 덮어씀
        public virtual string TitleText(TutorialContext ctx) => ctx.Format(title);
        public virtual string BodyText(TutorialContext ctx) => ctx.Format(body);

        // 지금 상황에서 할 수 없는 단계면 false (예: 이 캐릭터엔 그 스킬이 없음) → 조용히 건너뜀
        public virtual bool ShouldRun(TutorialContext ctx) => true;

        // 카드를 띄우기 직전 준비 (실행 중 값 초기화, 적 소환 등)
        public virtual void Begin(TutorialContext ctx) { }

        // 시간이 멈춰 있을 때 이 입력이 들어오면 시간이 다시 흐름. 기본 = 확인(Enter / 카드 클릭)
        public virtual bool IsResumeInput(TutorialContext ctx) => ctx.Input.ConfirmPressed;

        public string ResumeHint(TutorialContext ctx) =>
            ctx.Format(string.IsNullOrEmpty(resumeHint) ? DefaultResumeHint : resumeHint);

        protected virtual string DefaultResumeHint => "{Confirm} 계속";

        // 시간이 다시 흐른 뒤 매 프레임 불림. true 가 되면 다음 단계로
        public abstract bool IsDone(TutorialContext ctx);

        // 목표 카드에 붙는 진행 글 (예: "2 / 3"). null 이면 안 보임
        public virtual string Progress(TutorialContext ctx) => null;

        // 끝났거나 건너뛸 때 (소환한 적 정리 등)
        public virtual void End(TutorialContext ctx) { }

        // 코드/에디터 메뉴에서 기본 단계를 만들 때 쓰는 설정 (에셋은 인스펙터에서 바꾸면 됨)
        public TutorialStep Configure(string title, string body, string anchorId = "", bool pauseTime = true, string resumeHint = "")
        {
            this.title = title;
            this.body = body;
            this.anchorId = anchorId;
            this.pauseTime = pauseTime;
            this.resumeHint = resumeHint;
            name = GetType().Name + "_" + title;
            return this;
        }
    }
}
