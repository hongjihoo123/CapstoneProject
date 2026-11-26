using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Tutorial
{
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

        public virtual string TitleText(TutorialContext ctx) => ctx.Format(title);
        public virtual string BodyText(TutorialContext ctx) => ctx.Format(body);

        public virtual bool ShouldRun(TutorialContext ctx) => true;

        public virtual void Begin(TutorialContext ctx) { }

        public virtual bool IsResumeInput(TutorialContext ctx) => ctx.Input.ConfirmPressed;

        public string ResumeHint(TutorialContext ctx) =>
            ctx.Format(string.IsNullOrEmpty(resumeHint) ? DefaultResumeHint : resumeHint);

        protected virtual string DefaultResumeHint => "{Confirm} 계속";

        public abstract bool IsDone(TutorialContext ctx);

        public virtual string Progress(TutorialContext ctx) => null;

        public virtual void End(TutorialContext ctx) { }

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
