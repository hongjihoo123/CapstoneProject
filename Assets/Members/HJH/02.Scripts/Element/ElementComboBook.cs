using System.Collections.Generic;
using Members.JJH._02_Scripts.ElementsSystem;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Element
{
    // Read-only view of JJH's combo table (ElementBuffController.combos), shared by the combo chain and the HUD.
    // Read through the controller's public accessors once instead of copied:
    // whatever is set in that controller's Inspector is exactly what the game uses.
    public class ElementComboBook
    {
        public readonly struct Recipe
        {
            public readonly string Name;
            public readonly ElementType[] Sequence;

            public Recipe(string name, ElementType[] sequence)
            {
                Name = name;
                Sequence = sequence;
            }

            public int Length => Sequence.Length;

            // The combo is landed when the LAST inputs of the chain spell it.
            public bool EndsOn(IReadOnlyList<ElementType> inputs) => TailMatches(inputs, Sequence.Length);

            // How many of this combo's opening inputs the chain currently ends with (0 = not started).
            // A finished combo does not count as progress.
            public int TailProgress(IReadOnlyList<ElementType> inputs)
            {
                for (int k = Mathf.Min(inputs.Count, Sequence.Length - 1); k > 0; k--)
                {
                    if (TailMatches(inputs, k))
                        return k;
                }
                return 0;
            }

            private bool TailMatches(IReadOnlyList<ElementType> inputs, int length)
            {
                if (length > inputs.Count || length > Sequence.Length)
                    return false;

                int offset = inputs.Count - length;
                for (int i = 0; i < length; i++)
                {
                    if (inputs[offset + i] != Sequence[i])
                        return false;
                }
                return true;
            }
        }

        private readonly List<Recipe> _recipes = new();

        public IReadOnlyList<Recipe> Recipes => _recipes;
        public int LongestLength { get; private set; }

        public static ElementComboBook From(ElementBuffController controller)
        {
            var book = new ElementComboBook();
            if (controller == null)
                return book;

            for (int i = 0; i < controller.ComboCount; i++)
            {
                string name = controller.GetComboName(i);
                IReadOnlyList<ElementType> source = controller.GetComboSequence(i);
                if (source == null || source.Count == 0)
                    continue;

                var sequence = new ElementType[source.Count];
                for (int k = 0; k < sequence.Length; k++)
                    sequence[k] = source[k];

                book._recipes.Add(new Recipe(string.IsNullOrEmpty(name) ? string.Join("-", sequence) : name, sequence));
                book.LongestLength = Mathf.Max(book.LongestLength, sequence.Length);
            }

            return book;
        }

        // Longest combo the chain ends on wins (a 4-input combo beats a 1-input combo hidden in its tail).
        public bool TryFindComboAtEnd(IReadOnlyList<ElementType> inputs, out Recipe found)
        {
            found = default;
            bool any = false;
            foreach (Recipe recipe in _recipes)
            {
                if (recipe.EndsOn(inputs) && (!any || recipe.Length > found.Length))
                {
                    found = recipe;
                    any = true;
                }
            }
            return any;
        }

        // The combo the chain is furthest into (longest progress, then shortest combo = closest to done).
        public bool TryFindBestProgress(IReadOnlyList<ElementType> inputs, out Recipe found, out int progress)
        {
            found = default;
            progress = 0;
            foreach (Recipe recipe in _recipes)
            {
                int k = recipe.TailProgress(inputs);
                if (k > progress || (k == progress && k > 0 && recipe.Length < found.Length))
                {
                    found = recipe;
                    progress = k;
                }
            }
            return progress > 0;
        }

        public static string KoreanName(ElementType element) => element switch
        {
            ElementType.Fire => "불",
            ElementType.Water => "물",
            ElementType.Wind => "바람",
            ElementType.Electric => "전기",
            ElementType.Earth => "땅",
            _ => element.ToString()
        };
    }
}
