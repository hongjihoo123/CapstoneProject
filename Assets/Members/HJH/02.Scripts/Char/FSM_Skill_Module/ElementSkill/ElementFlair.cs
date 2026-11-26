using Assets.Members.HJH._02.Scripts.Char.Visual;
using Members.JJH._02_Scripts.ElementsSystem;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.ElementSkill
{
    public static class ElementFlair
    {
        private static bool HasPbr(ElementType element) => element == ElementType.Fire || element == ElementType.Wind;

        public static void Cast(ElementType element, Vector3 position, Vector3 direction, Color color)
        {
            if (element == ElementType.Fire)
                LightFlash.Flash(position, color, 2.5f, 2.5f, 0.12f);
        }

        public static void Impact(ElementType element, Vector3 ground, float radius, Color color)
        {
            float r = Mathf.Max(radius, 0.8f);
            if (!HasPbr(element))
            {
                Fx.Shockwave(ground, color, r * 1.3f);
                return;
            }

            if (element == ElementType.Fire)
                LightFlash.Flash(ground + Vector3.up * 0.8f, color, r * 3f, 6f, 0.35f);
        }

        public static void Hit(ElementType element, Vector3 body, Vector3 push, Color color)
        {
            LightFlash.Flash(body, color, 3.5f, 3f, 0.15f);
            if (!HasPbr(element))
                Fx.Hit(body, push, color, true);
        }

        public static void Landed(Color color, float shake, int hits)
        {
            float strength = Mathf.Clamp(shake * 0.6f + 0.04f * Mathf.Min(hits, 5), 0.1f, 0.5f);
            ScreenPulse.Play(color, strength, 0.24f);
        }
    }
}
