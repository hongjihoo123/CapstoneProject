using Assets.Members.HJH._02.Scripts.Char.Visual;
using Members.JJH._02_Scripts.ElementsSystem;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.ElementSkill
{
    // 원소 스킬 공통 연출 층. 입자는 스킬 에셋의 PBR 이펙트(castFx/impactFx/hitFx)가 맡고,
    // 여기는 주변을 비추는 빛과 화면만 더함 (프리팹 생성·연결: HJH > Element FX > PBR 이펙트 만들기).
    //  - 시전·폭발의 화면/카메라는 모든 스킬 공통 SkillPresentation 이 맡음
    //  - 불·바람이 아닌 원소는 아직 PBR 프리팹이 없어서 기존 납작한 연출을 유지
    // 스킬 코드는 ElementSkillCast 를 통해서만 부른다. 연출이 실패해도 판정에는 영향 없음.
    public static class ElementFlair
    {
        private static bool HasPbr(ElementType element) => element == ElementType.Fire || element == ElementType.Wind;

        // 쓸 때 손/몸에서
        public static void Cast(ElementType element, Vector3 position, Vector3 direction, Color color)
        {
            if (element == ElementType.Fire)
                LightFlash.Flash(position, color, 2.5f, 2.5f, 0.12f);   // 손에 불이 붙는 순간 주변이 밝아짐
        }

        // 터지는 자리 (범위 스킬)
        public static void Impact(ElementType element, Vector3 ground, float radius, Color color)
        {
            float r = Mathf.Max(radius, 0.8f);
            if (!HasPbr(element))
            {
                Fx.Shockwave(ground, color, r * 1.3f);
                return;
            }

            // 불 폭발은 땅과 주변을 크게 비춤 (PBR 배경이 빛을 받아 묵직해 보임)
            if (element == ElementType.Fire)
                LightFlash.Flash(ground + Vector3.up * 0.8f, color, r * 3f, 6f, 0.35f);
        }

        // 맞은 적 몸마다
        public static void Hit(ElementType element, Vector3 body, Vector3 push, Color color)
        {
            LightFlash.Flash(body, color, 3.5f, 3f, 0.15f);
            if (!HasPbr(element))
                Fx.Hit(body, push, color, true);
        }

        // 적을 맞혔을 때 화면 번쩍 (세게 흔드는 스킬일수록, 많이 맞힐수록)
        public static void Landed(Color color, float shake, int hits)
        {
            float strength = Mathf.Clamp(shake * 0.6f + 0.04f * Mathf.Min(hits, 5), 0.1f, 0.5f);
            ScreenPulse.Play(color, strength, 0.24f);
        }
    }
}
