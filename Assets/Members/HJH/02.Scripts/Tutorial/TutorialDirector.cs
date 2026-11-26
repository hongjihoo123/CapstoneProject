using System.Collections;
using Assets.Members.HJH._02.Scripts.Char.Character;
using Assets.Members.HJH._02.Scripts.SkillSwap;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Assets.Members.HJH._02.Scripts.Tutorial
{
    [DefaultExecutionOrder(1000)]
    public sealed class TutorialDirector : MonoBehaviour
    {
        private const float SkipAllHold = 1f;

        private TutorialSequence _sequence;
        private TutorialContext _ctx;
        private TutorialView _view;
        private bool _paused;
        private float _tabHeld;
        private bool _skipStep;
        private bool _finished;
        private TutorialStep _activeStep;

        public static TutorialDirector Current { get; private set; }
        public bool IsPaused => _paused;
        public TutorialStep CurrentStep => _activeStep;
        public TutorialContext Context => _ctx;

        public static TutorialDirector Run(TutorialSequence sequence)
        {
            if (Current != null)
                Destroy(Current.gameObject);

            var go = new GameObject("TutorialDirector");
            var director = go.AddComponent<TutorialDirector>();
            director._sequence = sequence;
            director._ctx = new TutorialContext();
            director._view = TutorialView.Attach(director._ctx);
            if (director._view == null)
            {
                Destroy(go);
                return null;
            }
            director._ctx.View = director._view;
            if (sequence.CardAtBottom)
                director._view.DockBottom(sequence.CardOffset);
            Current = director;
            return director;
        }

        private void Update()
        {
            if (_ctx == null)
                return;

            _ctx.Tick(_paused);
            UpdateSkip();
        }

        private void UpdateSkip()
        {
            Keyboard k = Keyboard.current;
            if (k == null || _finished)
                return;

            if (k.tabKey.isPressed)
            {
                _tabHeld += Time.unscaledDeltaTime;
                _view.SetSkipFill(_tabHeld / SkipAllHold);
                if (_tabHeld >= SkipAllHold)
                {
                    Debug.Log("[Tutorial] Tab 길게 → 튜토리얼 종료");
                    TutorialSession.Abort();
                    Shutdown();
                }
            }
            else
            {
                if (_tabHeld > 0f && _tabHeld < SkipAllHold)
                    _skipStep = true;
                _tabHeld = 0f;
                _view.SetSkipFill(0f);
            }
        }

        private IEnumerator Start()
        {
            yield return null;
            yield return null;

            if (_holdEnemies)
                _ctx.HoldSceneEnemies(true);

            var steps = _sequence.Steps;
            _ctx.StepCount = steps.Count;

            for (int i = 0; i < steps.Count; i++)
            {
                TutorialStep step = steps[i];
                if (step == null || !step.ShouldRun(_ctx))
                    continue;

                _ctx.StepIndex = i;
                _skipStep = false;
                Debug.Log($"[Tutorial] {i + 1}/{steps.Count} {step.name}");

                _activeStep = step;
                step.Begin(_ctx);
                _ctx.TryGetAnchor(step.AnchorId, out RectTransform anchor);
                _view.SetAnchor(anchor);

                if (step.PauseTime)
                {
                    SetPaused(true);
                    _view.ShowFull(step);
                    yield return null;
                    while (!_skipStep && !step.IsResumeInput(_ctx))
                    {
                        if (PlayerGone())
                            yield break;
                        yield return null;
                    }
                    SetPaused(false);
                    _view.ShowObjective(step, pop: false);
                }
                else
                {
                    _view.ShowFull(step, waiting: false);
                    _view.SetProgress(step.Progress(_ctx));
                    yield return null;
                }

                while (!_skipStep && !step.IsDone(_ctx))
                {
                    if (PlayerGone())
                        yield break;
                    _view.SetProgress(step.Progress(_ctx));
                    if (anchor == null && _ctx.TryGetAnchor(step.AnchorId, out anchor))
                        _view.SetAnchor(anchor);
                    yield return null;
                }

                step.End(_ctx);
                _activeStep = null;
                _view.SetAnchor(null);
                _view.SetWorldTarget(null, false);
                _view.FlashDone();
                yield return Wait(0.45f);
            }

            Finish();
        }

        private void Finish()
        {
            _finished = true;
            _view.Hide();

            string next = _sequence.SceneWhenFinished;
            if (string.IsNullOrEmpty(next))
                return;

            Debug.Log("[Tutorial] 튜토리얼 완료 → " + next);
            TutorialSession.Complete();
            Shutdown();

            Time.timeScale = 1f;
            RunLoadout.Clear();
            CharacterSelection.Clear();
            SceneManager.LoadScene(next);
        }

        private bool PlayerGone()
        {
            if (_ctx.Player == null || _ctx.Player.IsAlive)
                return false;

            Debug.Log("[Tutorial] 플레이어 사망 → 튜토리얼 종료");
            TutorialSession.Abort();
            Shutdown();
            return true;
        }

        private void SetPaused(bool paused)
        {
            if (_paused == paused)
                return;

            _paused = paused;
            Time.timeScale = paused ? 0f : 1f;
            _view.SetPaused(paused);
        }

        public TutorialView View => _view;

        public void HoldSpawnersNow()
        {
            _ctx.HoldStageSpawners(true);
            _holdEnemies = true;
        }

        private bool _holdEnemies;

        private static IEnumerator Wait(float seconds)
        {
            float end = Time.unscaledTime + seconds;
            while (Time.unscaledTime < end)
                yield return null;
        }

        private void Shutdown()
        {
            _finished = true;
            StopAllCoroutines();
            if (_paused)
                SetPaused(false);
            _ctx?.Dispose();
            if (_activeStep != null && _ctx != null)
                _activeStep.End(_ctx);
            _activeStep = null;
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (_view != null)
                _view.Detach();
            if (Current == this)
                Current = null;
            if (_paused)
                Time.timeScale = 1f;
            _ctx?.Dispose();
        }
    }
}
