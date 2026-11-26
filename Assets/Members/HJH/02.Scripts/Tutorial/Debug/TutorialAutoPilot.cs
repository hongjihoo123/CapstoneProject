#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Text;
using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using Assets.Members.HJH._02.Scripts.Quest;
using Assets.Members.HJH._02.Scripts.UI;
using Members.JJH._02_Scripts.Agents.Enemies;
using Members.JJH._02_Scripts.ElementsSystem;
using Members.KYR._01.Scripts.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Assets.Members.HJH._02.Scripts.Tutorial
{
    // 에디터 전용 확인 도구: 메인 화면 → "예" → 픽 화면 → 스테이지 → 클리어 → 메인 화면까지 튜토리얼을 입력 없이 돌림.
    // 메뉴 HJH/튜토리얼/자동 진행 데모 로 Captures/_tutorial_auto.txt 를 만들어 두면 다음 플레이에서 한 번 돎.
    //  - 튜토리얼 확인 입력(Enter/Space)은 TutorialInput.DebugPress, 스킬은 실제 스킬 입력 경로(SkillStateModule.ResolveInput)로
    //  - 적 처치는 데미지를 직접 줌 (조준 없이)
    //  - 카드마다 Captures/tutorial_auto/NN_단계.png 저장, 진행 기록은 Captures/_tutorial_auto_log.txt
    public sealed class TutorialAutoPilot : MonoBehaviour
    {
        private const string Flag = "Captures/_tutorial_auto.txt";
        private const string LogPath = "Captures/_tutorial_auto_log.txt";
        private const string ShotDir = "Captures/tutorial_auto";

        private readonly StringBuilder _log = new();
        private TutorialStep _lastStep;
        private int _shot;
        private bool _menuAnswered;
        private bool _finished;
        private float _start;
        private PlayerAgent _watched;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            if (!File.Exists(Flag))
                return;
            File.Delete(Flag);
            var go = new GameObject("TutorialAutoPilot");
            DontDestroyOnLoad(go);
            go.AddComponent<TutorialAutoPilot>();
            if (Directory.Exists(ShotDir))
                foreach (string f in Directory.GetFiles(ShotDir, "*.png"))
                    File.Delete(f);
            Directory.CreateDirectory(ShotDir);
        }

        private void Log(string line)
        {
            string text = $"[{Time.unscaledTime - _start,6:0.0}s] {line}";
            _log.AppendLine(text);
            Debug.Log("[TutorialAuto] " + line);
            File.WriteAllText(LogPath, _log.ToString());
        }

        private void Shot(string name)
        {
            string path = $"{ShotDir}/{++_shot:00}_{name}.png";
            ScreenCapture.CaptureScreenshot(path);
            Log("shot " + path);
        }

        private IEnumerator Start()
        {
            _start = Time.unscaledTime;
            Log("start, scene " + SceneManager.GetActiveScene().name);

            while (!_finished)
            {
                string scene = SceneManager.GetActiveScene().name;
                MainMenuScreen menu = FindFirstObjectByType<MainMenuScreen>();
                if (menu != null)
                {
                    yield return Wait(1.5f);
                    if (!_menuAnswered)
                    {
                        Shot("menu_question");
                        yield return Wait(0.5f);
                        _menuAnswered = true;
                        Log("메인 화면: 처음인가요? → 예");
                        menu.DebugAnswer(true);
                        yield return Wait(2f);
                        continue;
                    }

                    Shot("menu_after_tutorial");
                    Log($"메인 화면으로 돌아옴. 튜토리얼 완료 기록={TutorialSession.EverCompleted}, Active={TutorialSession.Active}");
                    Log("AUTO DONE");
                    _finished = true;
                    break;
                }

                TutorialDirector director = TutorialDirector.Current;
                if (director == null || director.CurrentStep == null)
                {
                    yield return null;
                    continue;
                }

                if (director.Context.Player != null && director.Context.Player != _watched)
                {
                    _watched = director.Context.Player;
                    foreach (SkillSlotId slot in SkillSlots.All)
                    {
                        SkillData data = _watched.SkillFsm != null ? _watched.SkillFsm.GetSkill(slot) : null;
                        Log($"  장착 {slot}: {(data != null ? data.name : "비어 있음")}");
                    }
                    _watched.DamageTaken += (amount, source) =>
                        Log($"  !! 플레이어 피격 {amount:0.#} from {(source != null ? source.name : "null")} (HP 남음 {(_watched.Health != null ? _watched.Health.CurrentHealth : -1f):0})");
                }

                TutorialStep step = director.CurrentStep;
                if (step != _lastStep)
                {
                    _lastStep = step;
                    Log($"{scene}: 단계 {director.Context.StepIndex + 1}/{director.Context.StepCount} {step.name} (paused={director.IsPaused}, timeScale={Time.timeScale})");
                    yield return Wait(0.6f);
                    Shot($"{scene}_{director.Context.StepIndex + 1:00}_{step.GetType().Name}");
                    yield return Wait(0.3f);
                }

                yield return Drive(director, step);
            }
        }

        private IEnumerator Drive(TutorialDirector director, TutorialStep step)
        {
            TutorialContext ctx = director.Context;

            if (director.IsPaused)
            {
                Log($"  시간 정지 확인 timeScale={Time.timeScale} → 확인 입력");
                ctx.Input.DebugPress(confirm: true);
                yield return null;
                yield return null;
                Log($"  재개 timeScale={Time.timeScale}");
                yield break;
            }

            switch (step)
            {
                case PickScreenStep pick:
                    yield return Wait(1f);
                    if (pick.Task == PickScreenStep.PickTask.Start)
                    {
                        Log("  픽 화면: 1번 캐릭터로 시작");
                        (ctx.Pick as CharacterPickScreen)?.DebugPick(0);
                        yield return Wait(3f);
                    }
                    else
                    {
                        ctx.Input.DebugPress(space: true);
                        yield return null;
                    }
                    break;

                case MoveStep:
                    if (ctx.Player != null)
                    {
                        Vector3 forward = ctx.Player.transform.forward;
                        forward.y = 0f;
                        ctx.Player.Teleport(ctx.Player.transform.position + forward.normalized * 0.15f);
                    }
                    yield return null;
                    break;

                case ActionStep action:
                    if (action.Kind == ActionStep.ActionKind.BasicAttack)
                    {
                        ctx.DebugCountBasicAttack();
                        Log("  기본 공격 1회 (카운트)");
                    }
                    else
                    {
                        yield return Cast(ctx, action.Slot);
                    }
                    yield return Wait(0.8f);
                    yield return KillTargets(ctx);
                    yield return Wait(1.5f);
                    break;

                case ComboStep combo:
                    var recipe = combo.Planned;
                    if (recipe.Sequence != null)
                    {
                        Log($"  콤보 {recipe.Name} 시도");
                        foreach (ElementType element in recipe.Sequence)
                        {
                            if (ctx.TryGetSlotFor(element, out SkillSlotId slot))
                                yield return Cast(ctx, slot);
                            yield return Wait(1.0f);
                        }
                        Log($"  콤보 성공 수={ctx.CombosLanded}");
                    }
                    yield return KillTargets(ctx);
                    yield return Wait(2f);
                    break;

                case ComboBindStep:
                {
                    // 상점 앞으로 가서 창을 열고 불 → 물 바인딩
                    var station = FindFirstObjectByType<Element.ComboBindStation>();
                    if (station != null && ctx.Player != null)
                        ctx.Player.Teleport(station.transform.position + new Vector3(-1.5f, 0.3f, -1.5f));
                    yield return Wait(0.6f);
                    station?.Open();
                    ComboBindPanel panel = ComboBindPanel.Current;
                    if (panel != null)
                    {
                        while (panel.Sequence.Count > 0)
                            panel.RemoveLast();
                        panel.Add(ElementType.Fire);
                        panel.Add(ElementType.Water);
                        Shot("combo_bind");
                        yield return Wait(0.5f);
                        Log($"  콤보 바인딩 저장 {panel.Save()}");
                    }
                    yield return Wait(1.5f);
                    break;
                }

                case ClearRoomStep:
                    yield return ClearRoom(ctx);
                    break;

                case TargetWaveStep:
                    yield return Wait(0.8f);
                    yield return KillTargets(ctx);
                    yield return Wait(1.5f);
                    break;

                default:
                    yield return null;
                    break;
            }
        }

        // 실제 스킬 입력 경로: 누름 → 잠깐 누르고 있음 → 뗌 (누르면 바로 나가는 칸 / 떼면 나가는 칸 모두)
        private IEnumerator Cast(TutorialContext ctx, SkillSlotId slot)
        {
            SkillStateModule skills = ctx.Player != null ? ctx.Player.SkillFsm : null;
            if (skills == null)
                yield break;

            int before = ctx.SkillUses(slot);
            var input = new FakeInput(slot);
            for (int frame = 0; frame < 6; frame++)
            {
                input.Frame = frame;
                skills.ResolveInput(input);
                yield return null;
            }
            Log($"  스킬 {slot} 입력 → 사용 {before} → {ctx.SkillUses(slot)}");
        }

        private IEnumerator KillTargets(TutorialContext ctx)
        {
            foreach (RoomClearIgnore target in FindObjectsByType<RoomClearIgnore>(FindObjectsSortMode.None))
            {
                var enemy = target.GetComponentInChildren<AbstractEnemy>();
                if (enemy != null && enemy.IsAlive)
                {
                    enemy.TakeDamage(999999f, ctx.Player != null ? ctx.Player.gameObject : null);
                    Log("  연습용 적 처치: " + target.name);
                }
            }
            yield return null;
        }

        private IEnumerator ClearRoom(TutorialContext ctx)
        {
            // 확인 도구라 플레이어는 죽지 않게 채워 줌 (조준 없이 적 사이로 순간이동하니 맞기만 함)
            ctx.Player?.Heal(1000f);

            // 아직 안 터진 스포너로 가서 적을 불러내고, 나온 적을 처치
            if (ctx.TryGetGuideTarget(out Vector3 point) && ctx.Player != null)
            {
                ctx.Player.Teleport(point + Vector3.up * 0.5f + Vector3.back * 2f);
                Log($"  이동 → {point}, 남은 적 {(ctx.Tracker != null ? ctx.Tracker.Total - ctx.Tracker.Kills : -1)}");
            }
            yield return Wait(1.5f);

            // 한 방에 죽이면 보스는 페이즈 전환을 건너뛰어 죽음 처리가 안 됨 → 최대 체력의 20% 씩 나눠 때림 (실제 전투처럼)
            for (int hit = 0; hit < 8; hit++)
            {
                foreach (AbstractEnemy enemy in FindObjectsByType<AbstractEnemy>(FindObjectsSortMode.None))
                {
                    if (enemy != null && enemy.IsAlive && enemy.EnemyData != null)
                        enemy.TakeDamage(enemy.EnemyData.Health * 0.2f + 1f, ctx.Player != null ? ctx.Player.gameObject : null);
                }
                yield return Wait(0.6f);
            }
            Log($"  처치 후 {ctx.Tracker?.Kills}/{ctx.Tracker?.Total} cleared={ctx.Tracker?.IsCleared}");
        }

        private static IEnumerator Wait(float seconds)
        {
            float end = Time.unscaledTime + seconds;
            while (Time.unscaledTime < end)
                yield return null;
        }

        private sealed class FakeInput : ISkillInputSource
        {
            private readonly SkillSlotId _slot;
            public int Frame;
            public FakeInput(SkillSlotId slot) => _slot = slot;
            public bool WasSkillPressed(SkillSlotId slot) => slot == _slot && Frame == 0;
            public bool IsSkillHeld(SkillSlotId slot) => slot == _slot && Frame < 3;
            public bool WasSkillReleased(SkillSlotId slot) => slot == _slot && Frame == 3;
            public bool WasCancelPressed() => false;
            public bool WasJumpPressed() => false;
        }
    }
}
#endif
