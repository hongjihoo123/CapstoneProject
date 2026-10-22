using System;
using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.Element;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.ElementSkill
{
    // 이펙트 칸 하나. tint 켜면 원소 색으로 물들임 (끄면 프리팹 원래 색)
    [Serializable]
    public class FxSlot
    {
        public GameObject prefab;
        public bool tint;
        public bool IsSet => prefab != null;
    }

    // 원소 스킬(교체 스킬) 공통 부모. 스킬 모양마다 자식 클래스 하나씩 있음 (Skills 폴더)
    //   CircleSkill, ProjectileSkill, LineSkill, ConeSkill, ZoneSkill, DashSkill, SelfSkill
    // 여기엔 모든 모양이 다 쓰는 것만 있음: 맞은 적한테 할 것, 손맛, 이펙트
    // 자식은 Cast() 하나만 만들면 됨 → "누구를 맞히나" 찾아서 cast.Hit(적) 해주면 끝
    public abstract class ElementSkillBase : SkillData
    {
        public enum DamageScaling { AD, AP }
        public enum ImpactStyle { None, Explosion, Slam, Shockwave, Lightning }

        [Header("맞은 적한테 (위에서부터 순서대로)")]
        [SerializeField] private List<HitEffect> onHit = new() { new HitEffect { kind = HitEffectKind.Damage, value = 20f } };
        [SerializeField, Tooltip("AD/AP 나뉘면 쓸 거. 지금은 둘 다 공격력")]
        private DamageScaling scaling = DamageScaling.AP;

        [Header("손맛")]
        [SerializeField, Tooltip("맞았을 때 화면 멈추는 시간 (많이 맞히면 더 길어짐)")] private float hitStop = 0.05f;
        [SerializeField, Tooltip("맞았을 때 화면 흔들림")] private float shake = 0.2f;

        [Header("이펙트")]
        [SerializeField, Tooltip("쓸 때 내 손/몸에서")] private FxSlot castFx = new();
        [SerializeField, Tooltip("터지는 자리")] private FxSlot impactFx = new();
        [SerializeField, Tooltip("터질 때 같이 나오는 기본 이펙트 (프리팹 없어도 이건 나옴)")]
        private ImpactStyle impactStyle = ImpactStyle.Explosion;
        [SerializeField, Tooltip("맞은 적 몸마다")] private FxSlot hitFx = new();
        [SerializeField, Tooltip("CC 걸린 적 머리 위, CC 시간 동안")] private FxSlot statusFx = new();

        public IReadOnlyList<HitEffect> OnHit => onHit;
        public DamageScaling Scaling => scaling;
        public float HitStop => hitStop;
        public float Shake => shake;
        public FxSlot CastFx => castFx;
        public FxSlot ImpactFx => impactFx;
        public ImpactStyle Style => impactStyle;
        public FxSlot HitFx => hitFx;
        public FxSlot StatusFx => statusFx;

        // 이펙트 색 = 원소 색 (에셋에서 fxColor 따로 정하면 그거)
        protected override Color DefaultFxColor =>
            ElementPalette.Default != null ? ElementPalette.Default.ColorOf(Element) : base.DefaultFxColor;

        public sealed override ISkillExecution Begin(ISkillContext context) => Cast(new ElementSkillCast(this, context));

        // 여기만 만들면 됨. 여러 프레임 걸리면 ISkillExecution 돌려주고, 한 번에 끝나면 null
        protected abstract ISkillExecution Cast(ElementSkillCast cast);

        public override void DescribeStats(List<SkillStat> stats)
        {
            DescribeShape(stats);
            SkillEffects.Describe(stats, onHit, SelfEffects);
            stats.Add(new SkillStat("계수", scaling.ToString()));
            base.DescribeStats(stats);
        }

        // 툴팁에 모양 숫자 (범위, 사거리 같은 거)
        protected abstract void DescribeShape(List<SkillStat> stats);

        // 나한테 거는 효과 있는 스킬(장판, 버프)만 override
        protected virtual IReadOnlyList<SelfEffect> SelfEffects => Array.Empty<SelfEffect>();
    }
}
