using UnityEngine;

namespace RobotWeapons
{
    // 이번 피격의 색. 원소 스킬이 피해를 주기 직전에 표시하면, 같은 프레임에 연출 쪽(CombatFeedback)이
    // 읽어서 불꽃·데미지 숫자를 원소 색으로 그린다. (HitPoint 와 같은 방식, PlayerAgent 는 몰라도 됨)
    public static class HitTint
    {
        private static IDamageable _target;
        private static Color _color;
        private static int _frame = -1;

        public static void Mark(IDamageable target, Color color)
        {
            _target = target;
            _color = color;
            _frame = Time.frameCount;
        }

        // 같은 프레임, 같은 대상이면 true. 한 번의 적중에 피해가 여러 번 나가도 같은 색이 되도록 지우지 않는다
        public static bool TryGet(IDamageable target, out Color color)
        {
            color = _color;
            return _frame == Time.frameCount && ReferenceEquals(_target, target);
        }
    }
}
