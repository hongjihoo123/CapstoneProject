#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using Assets.Members.HJH._02.Scripts.Hud;
using Assets.Members.HJH._02.Scripts.UI;
using Members.JJH._02_Scripts.ElementsSystem;
using Members.KYR._01.Scripts.Player;
using Members.KYR._01.Scripts.Stats;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Element
{
    // 에디터 전용 확인 도구: 시작 콤보 교체 → 상점 F 창 열기 → 바인딩 저장 → 콤보 발동 → 다시 바인딩(교체) 를 입력 없이 돌림.
    // 메뉴 HJH/콤보/바인딩 자동 확인 (다음 플레이 한 번) 으로 플래그를 만든 뒤 Map_Shop 플레이.
    // 기록 Captures/_combo_bind_log.txt, 화면 Captures/combo_bind/*.png
    public sealed class ComboBindSelfTest : MonoBehaviour
    {
        public const string Flag = "Captures/_combo_bind_test.txt";
        private const string LogPath = "Captures/_combo_bind_log.txt";
        private const string ShotDir = "Captures/combo_bind";

        private readonly StringBuilder _log = new();
        private int _shot;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (!File.Exists(Flag))
                return;
            File.Delete(Flag);
            Directory.CreateDirectory(ShotDir);
            foreach (string f in Directory.GetFiles(ShotDir, "*.png"))
                File.Delete(f);
            new GameObject("ComboBindSelfTest").AddComponent<ComboBindSelfTest>();
        }

        private void Log(string line)
        {
            _log.AppendLine($"[{Time.unscaledTime,6:0.0}s] {line}");
            Debug.Log("[ComboBindTest] " + line);
            File.WriteAllText(LogPath, _log.ToString());
        }

        private IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot($"{ShotDir}/{++_shot:00}_{name}.png");
            yield return new WaitForSecondsRealtime(0.3f);
        }

        private IEnumerator Start()
        {
            yield return new WaitForSecondsRealtime(2.5f);

            PlayerAgent player = FindFirstObjectByType<PlayerAgent>();
            ElementComboChain chain = FindFirstObjectByType<ElementComboChain>();
            ComboBindStation station = FindFirstObjectByType<ComboBindStation>();
            PlayerHudSource hud = PlayerHudSource.Instance;
            if (player == null || chain == null || station == null)
            {
                Log($"FAIL 준비 안 됨 player={player != null} chain={chain != null} station={station != null}");
                yield break;
            }

            SkillStateModule skills = player.SkillFsm;
            int landed = 0;
            string lastLanded = "";
            chain.ComboLanded += c => { landed++; lastLanded = c.Combo.Name; };

            Log($"캐릭터 우클릭 {Elem(skills.GetSkill(SkillSlotId.Weapon))}, 궁극기 {Elem(skills.GetSkill(SkillSlotId.Ultimate))}");
            Log($"시작 콤보 {ComboLoadout.NameOf(ComboLoadout.Starter)} / 표 [{Book(chain)}] / HUD 목록 {hud?.ComboList.Count}");

            // 1) 시작 콤보 교체 (픽 화면의 두 번째 선택지)
            var options = ComboLoadout.StarterOptions(skills.GetSkill(SkillSlotId.Weapon), skills.GetSkill(SkillSlotId.Ultimate));
            ComboLoadout.ChooseStarter(options.Find(o => ComboLoadout.NameOf(o) != ComboLoadout.NameOf(ComboLoadout.Starter)) ?? options[0]);
            Log($"시작 콤보 → {ComboLoadout.NameOf(ComboLoadout.Starter)} / 표 [{Book(chain)}]");

            // 2) 상점 앞으로 가서 창 열기
            player.Teleport(station.transform.position + new Vector3(-1.5f, 0.3f, -1.5f));
            yield return new WaitForSecondsRealtime(0.6f);
            Log($"상점 근처 {station.PlayerNear}");
            station.Open();
            ComboBindPanel panel = ComboBindPanel.Current;
            Log($"창 열림 {panel != null}, timeScale {Time.timeScale}");
            panel.Add(ElementType.Fire);
            yield return Shot("bind_add_fire");
            panel.Add(ElementType.Water);
            panel.Add(ElementType.Wind);
            yield return Shot("bind_fire_water_wind");
            Log("미리보기 " + ComboLoadout.Evaluate(panel.Sequence).Summary());

            // 3) 저장
            bool saved = panel.Save();
            yield return new WaitForSecondsRealtime(1f);
            Log($"저장 {saved}, 바인딩 {ComboLoadout.NameOf(ComboLoadout.Bound)}, 창 닫힘 {!ComboBindPanel.IsOpen}, timeScale {Time.timeScale}");
            Log($"표 [{Book(chain)}] / HUD 목록 {hud?.ComboList.Count} / ComboUI 줄 {ComboUiRows()}");

            // 4) 시작 콤보를 실제 스킬로 발동 (우클릭/궁극기)
            float damageBefore = player.Stats.Get(PlayerStatId.Damage);
            float speedBefore = player.Stats.Get(PlayerStatId.AttackSpeed);
            foreach (ElementType element in ComboLoadout.Starter.ToArray())
            {
                SkillSlotId slot = Elem(skills.GetSkill(SkillSlotId.Weapon)) == element.ToString() ? SkillSlotId.Weapon : SkillSlotId.Ultimate;
                skills.Equip(slot, skills.GetSkill(slot));   // 쿨타임 비움
                yield return Cast(skills, slot);
                yield return new WaitForSecondsRealtime(slot == SkillSlotId.Ultimate ? 2.5f : 1f);
            }
            chain.TryGetEffectRemaining(ComboLoadout.NameOf(ComboLoadout.Starter), out float left);
            Log($"시작 콤보 발동: 완성 {landed}회 (마지막 {lastLanded}), 효과 남은 시간 {left:0.0}s, 공격력 {damageBefore:0.##}→{player.Stats.Get(PlayerStatId.Damage):0.##}, 공속 {speedBefore:0.##}→{player.Stats.Get(PlayerStatId.AttackSpeed):0.##}");
            yield return Shot("starter_landed");

            // 5) 바인딩 콤보 (거너는 물/바람 스킬이 없어서 입력만 넣어 봄)
            float magicBefore = player.Stats.Get(PlayerStatId.MagicDamage);
            float moveBefore = player.Stats.Get(PlayerStatId.MoveSpeed);
            foreach (ElementType element in new[] { ElementType.Fire, ElementType.Water, ElementType.Wind })
                AddInput(chain, element);
            yield return new WaitForSecondsRealtime(0.5f);
            chain.TryGetEffectRemaining(ComboLoadout.NameOf(ComboLoadout.Bound), out left);
            Log($"바인딩 콤보 입력: 마지막 완성 {lastLanded}, 효과 남은 시간 {left:0.0}s, 마법력 {magicBefore:0.##}→{player.Stats.Get(PlayerStatId.MagicDamage):0.##}, 이속 {moveBefore:0.##}→{player.Stats.Get(PlayerStatId.MoveSpeed):0.##}");
            yield return Shot("bound_landed");

            // 6) 저장한 상점은 잠김: F(Open) → 창 대신 "기회를 소진하셨습니다."
            player.Teleport(station.transform.position + new Vector3(-1.5f, 0.3f, -1.5f));
            yield return new WaitForSecondsRealtime(0.4f);
            bool reopened = station.Open();
            Log($"상점 잠김 {station.Used}, 다시 열림 {reopened}, 창 {ComboBindPanel.IsOpen}");
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Shot("station_used_notice");

            // 7) 다시 바인딩 (교체) — 상점은 잠겼으니 창을 직접 열어서 (나중에 레벨업 보상 같은 경로)
            ComboBindPanel.Open(station.PanelPrefab);
            panel = ComboBindPanel.Current;
            panel.Clear();
            yield return new WaitForSecondsRealtime(0.3f);   // 창이 떠오르는 연출 끝까지
            panel.Add(ElementType.Earth);
            panel.Add(ElementType.Earth);
            panel.Add(ElementType.Electric);
            panel.Add(ElementType.Fire);
            yield return Shot("rebind_four_slots");
            panel.RemoveLast();
            panel.RemoveLast();
            yield return Shot("rebind_earth_earth");
            panel.Save();
            yield return new WaitForSecondsRealtime(1f);
            Log($"교체 후 바인딩 {ComboLoadout.NameOf(ComboLoadout.Bound)} / 표 [{Book(chain)}] / ComboUI 줄 {ComboUiRows()} / 이전 효과 남음 {chain.TryGetEffectRemaining("불 → 물 → 바람", out _)}");
            OpenComboList();
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Shot("after_swap_list");

            // 8) 길이 확인: 1칸/5칸은 저장 안 됨
            Log($"1칸 저장 가능 {ComboLoadout.CanBind(new[] { ElementType.Fire }, out string r1)} ({r1}), 5칸 {ComboLoadout.CanBind(new[] { ElementType.Fire, ElementType.Fire, ElementType.Fire, ElementType.Fire, ElementType.Fire }, out string r5)} ({r5})");
            Log("DONE");
        }

        private static string Elem(SkillData data) => data != null && data.TryGetElement(out ElementType e) ? e.ToString() : "없음";

        private static string Book(ElementComboChain chain) => string.Join(", ", chain.Book.Recipes.Select(r => r.Name));

        private static IEnumerator Cast(SkillStateModule skills, SkillSlotId slot)
        {
            var input = new FakeInput(slot);
            for (int frame = 0; frame < 6; frame++)
            {
                input.Frame = frame;
                skills.ResolveInput(input);
                yield return null;
            }
        }

        private static void AddInput(ElementComboChain chain, ElementType element)
        {
            MethodInfo add = typeof(ElementComboChain).GetMethod("AddInput", BindingFlags.Instance | BindingFlags.NonPublic);
            add?.Invoke(chain, new object[] { new ElementComboChain.ComboInput(element, SkillSlotId.Basic1) });
        }

        private static int ComboUiRows()
        {
            ComboUI ui = FindFirstObjectByType<ComboUI>(FindObjectsInactive.Include);
            FieldInfo rows = typeof(ComboUI).GetField("_rows", BindingFlags.Instance | BindingFlags.NonPublic);
            return ui != null && rows?.GetValue(ui) is System.Collections.ICollection list ? list.Count : -1;
        }

        private static void OpenComboList()
        {
            ComboUI ui = FindFirstObjectByType<ComboUI>(FindObjectsInactive.Include);
            if (ui == null)
                return;
            typeof(ComboUI).GetMethod("SetListOpen", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(ui, new object[] { true });
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
