using System.Collections.Generic;
using System.Text;
using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using Assets.Members.HJH._02.Scripts.Element;
using Members.JJH._02_Scripts.ElementsSystem;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Tutorial
{
    // 콤보: 지금 들고 있는 스킬로 칠 수 있는 콤보 하나를 골라 키 순서를 보여 주고, 실제로 콤보가 터지면 끝.
    //  - 고른 콤보 목록(ComboLoadout → 콤보 표) 중 모든 원소에 키가 있는 가장 짧은 콤보
    //  - 진행 중엔 "다음: [키]" 를 실시간으로
    //  - 칠 수 있는 콤보가 없으면 건너뜀
    [CreateAssetMenu(menuName = "HJH/Tutorial/Step - Combo", fileName = "Step_Combo")]
    public class ComboStep : TargetPracticeStep
    {
        [System.NonSerialized] private int _start;
        [System.NonSerialized] private string _comboLine;
        [System.NonSerialized] private ElementComboBook.Recipe _recipe;

        // 고른 콤보 (확인 도구용)
        public ElementComboBook.Recipe Planned => _recipe;

        public override bool ShouldRun(TutorialContext ctx) => ctx.Hud != null && ctx.Hud.HasCombo && TryPick(ctx, out _);

        public override void Begin(TutorialContext ctx)
        {
            _start = ctx.CombosLanded;
            _comboLine = TryPick(ctx, out _recipe) ? Describe(ctx, _recipe) : "";
            base.Begin(ctx);
        }

        public override bool IsResumeInput(TutorialContext ctx) => ctx.Input.ConfirmPressed;

        public override bool IsDone(TutorialContext ctx) => ctx.CombosLanded > _start && TargetDown;

        public override string Progress(TutorialContext ctx)
        {
            if (ctx.CombosLanded > _start)
                return TargetDown ? "<color=#D6B878>콤보 성공!</color>" : "<color=#D6B878>콤보 성공!</color>   ·   " + TargetProgress;

            if (ctx.Hud != null && ctx.Hud.TryGetNextCombo(out ElementType next, out ElementComboBook.Recipe combo, out int progress))
                return $"{combo.Name} {progress}/{combo.Length}   다음 {TutorialContext.Key(TutorialContext.PrettyKey(ctx.Hud.KeyFor(next)))}";
            return _comboLine;
        }

        // 카드 설명 뒤에 붙는 줄: "과전류  전기 [우클릭] → 전기 [우클릭]"
        public override string BodyText(TutorialContext ctx)
        {
            string body = base.BodyText(ctx);
            if (string.IsNullOrEmpty(_comboLine))
                return body;
            return string.IsNullOrEmpty(body) ? _comboLine : body + "\n" + _comboLine;
        }

        // 실제로 칠 수 있는 콤보만: 모든 원소에 스킬이 있고, 같은 스킬을 연달아 써야 하면 충전이 2개 이상이거나
        // 쿨타임이 콤보 이어치기 시간보다 넉넉히 짧아야 함 (예: 거너 우클릭 쿨 3초 = 이어치기 3초 → 과전류는 불가).
        // 그중 짧고, 궁극기를 안 쓰는 콤보를 먼저.
        private static bool TryPick(TutorialContext ctx, out ElementComboBook.Recipe best)
        {
            best = default;
            int bestScore = int.MaxValue;
            IReadOnlyList<ElementComboBook.Recipe> list = ctx.Hud != null ? ctx.Hud.ComboList : null;
            if (list == null)
                return false;

            float window = ctx.Hud.ComboWindow > 0f ? ctx.Hud.ComboWindow : 3f;
            foreach (ElementComboBook.Recipe recipe in list)
            {
                if (recipe.Sequence == null || recipe.Length < 2)
                    continue;

                int score = recipe.Length * 10;
                bool castable = true;
                var uses = new Dictionary<SkillSlotId, int>();
                SkillSlotId? previous = null;
                foreach (ElementType element in recipe.Sequence)
                {
                    if (!ctx.TryGetSlotFor(element, out var slot))
                    {
                        castable = false;
                        break;
                    }

                    var info = ctx.Hud.GetSlot(slot);
                    uses[slot] = (uses.TryGetValue(slot, out int n) ? n : 0) + 1;
                    // 한 번에 다 쓰는데 충전이 모자라면, 쿨타임이 이어치기 시간 안에 돌아와야 함
                    if (uses[slot] > Mathf.Max(1, info.MaxCharges) && info.CooldownDuration >= window * 0.8f)
                    {
                        castable = false;
                        break;
                    }

                    if (slot == SkillSlotId.Ultimate)
                        score += 100;
                    if (previous == slot)
                        score += 5;
                    previous = slot;
                }

                if (castable && score < bestScore)
                {
                    best = recipe;
                    bestScore = score;
                }
            }
            return bestScore < int.MaxValue;
        }

        private static string Describe(TutorialContext ctx, ElementComboBook.Recipe recipe)
        {
            var sb = new StringBuilder();
            sb.Append("<color=#E8EFF7>").Append(recipe.Name).Append("</color>  ");
            for (int i = 0; i < recipe.Length; i++)
            {
                if (i > 0)
                    sb.Append(" → ");
                ElementType element = recipe.Sequence[i];
                sb.Append(ElementComboBook.KoreanName(element)).Append(' ')
                  .Append(TutorialContext.Key(TutorialContext.PrettyKey(ctx.Hud.KeyFor(element))));
            }
            return sb.ToString();
        }
    }
}
