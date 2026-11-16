using Members.KYR._01.Scripts.Player;
using UnityEngine;

namespace RobotWeapons
{
    public enum CrowdControlType
    {
        Slow,       // 이동 속도 감소
        Root,       // 못 움직임 (공격은 함)
        Stun,       // 아무것도 못 함
        Knockback   // 밀려남
    }

    // CC 를 "받는" 쪽. 스킬은 이걸로 요청만 하고, 진짜로 걸지/몇 초 걸지/어떻게 멈출지는 받는 쪽이 정함
    // (남은 시간 vs 새 시간 큰 값, 면역, 보스 저항 같은 규칙이 전부 여기 한 곳에 있게)
    // 적 담당이 자기 적에 직접 구현해도 되고, 안 하면 CrowdControlReceiver 가 기본으로 붙음
    public interface ICrowdControlTarget
    {
        void ApplySlow(float percent, float duration);
        void ApplyRoot(float duration);
        void ApplyStun(float duration);
        void ApplyKnockback(Vector3 direction, float distance);
        bool Has(CrowdControlType type);
    }

    public static class CrowdControl
    {
        // 맞은 대상의 CC 받는 곳. 적이 직접 구현한 게 없으면 기본 Receiver 를 붙여서 씀
        public static ICrowdControlTarget Of(IDamageable target)
        {
            if (target is not Component component || component == null)
                return null;

            if (component.TryGetComponent(out ICrowdControlTarget existing))
                return existing;

            // 플레이어한테는 안 붙임 (플레이어 스킬이 플레이어를 칠 일은 없지만 혹시 몰라서)
            if (component.GetComponent<PlayerAgent>() != null)
                return null;

            return component.gameObject.AddComponent<CrowdControlReceiver>();
        }

        // 보스처럼 밀리거나 끌려가면 안 되는 대상인지. 밀치기·끌어당기기·밀어내기·타격 밀림이 전부 이걸 보고 건너뜀.
        // (CrowdControlReceiver 의 tier 는 아무도 안 넣어 줘서 Normal 로 남기 때문에, 보스는 BossEnemy 컴포넌트로도 판단)
        public static bool IsImmovable(Component target)
        {
            if (target == null)
                return false;

            if (target.TryGetComponent(out CrowdControlReceiver receiver) && receiver.EnemyTier == CrowdControlReceiver.Tier.Boss)
                return true;

            return target.GetComponentInParent<Members.JJH._02_Scripts.Agents.Enemies.BossEnemy>() != null;
        }
    }
}
