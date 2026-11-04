using System;
using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.ElementSkill;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Visual
{
    // 시전 몸 동작마다 "손을 뻗는 순간(release)" 시각. 굽기 도구가 손 속도가 가장 빠른 프레임으로 자동 기록
    // (HJH/애니/원소 스킬 시전 모션 굽기). Resources/HJH/CastMotions.asset 하나.
    // 스킬은 누르는 즉시 나가므로, 캐릭터 애니메이터가 이 시각 직전부터 클립을 틀어서 동작과 이펙트가 맞게 함.
    [CreateAssetMenu(menuName = "HJH/Cast Motion Set", fileName = "CastMotions")]
    public class CastMotionSet : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public CastMotion motion;
            [Tooltip("애니메이터 상태 이름 (Gunner.controller 0번 레이어)")]
            public string state;
            [Tooltip("손 뻗는 순간 (클립 기준 초)")]
            public float release;
            [Tooltip("클립 길이 (초)")]
            public float length;
            [Tooltip("손 뻗기 전 준비 동작을 얼마나 보여줄지 (초). 클립은 release - lead 부터 시작")]
            public float lead = 0.12f;
        }

        public Entry[] entries = Array.Empty<Entry>();

        private const string Path = "HJH/CastMotions";
        private static CastMotionSet _default;
        private static bool _loaded;

        public static CastMotionSet Default
        {
            get
            {
                if (!_loaded)
                {
                    _loaded = true;
                    _default = Resources.Load<CastMotionSet>(Path);
                }
                return _default;
            }
        }

        public Entry Of(CastMotion motion)
        {
            foreach (Entry e in entries)
                if (e != null && e.motion == motion)
                    return e;
            return null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _default = null;
            _loaded = false;
        }
    }
}
