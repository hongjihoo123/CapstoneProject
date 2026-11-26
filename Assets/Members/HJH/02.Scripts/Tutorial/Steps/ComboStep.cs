using System.Collections.Generic;
using System.Text;
using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using Assets.Members.HJH._02.Scripts.Element;
using Members.JJH._02_Scripts.ElementsSystem;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Tutorial
{
    [CreateAssetMenu(menuName = "HJH/Tutorial/Step - Combo", fileName = "Step_Combo")]
    public class ComboStep : TargetPracticeStep
    {
        [System.NonSerialized] private int _start;
        [System.NonSerialized] private string _comboLine;
        [System.NonSerialized] private ElementComboBook.Recipe _recipe;

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

        public override string BodyText(TutorialContext ctx)
        {
            string body = base.BodyText(ctx);
            if (string.IsNullOrEmpty(_comboLine))
                return body;
            return string.IsNullOrEmpty(body) ? _comboLine : body + "\n" + _comboLine;
        }

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
