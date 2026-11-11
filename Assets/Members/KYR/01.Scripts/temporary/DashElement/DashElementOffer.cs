using System;
using System.Reflection;
using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using Assets.Members.HJH._02.Scripts.Element;
using Members.JJH._02_Scripts.ElementsSystem;
using Members.KYR._01.Scripts.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Members.KYR._01.Scripts.DashElement
{
    public static class DashElementOffer
    {
        private static int _sceneHandle = int.MinValue;
        private static ElementType[] _options;
        private static SkillData _source;
        private static SkillData _clone;

        public static ElementType[] Current
        {
            get
            {
                Scene scene = SceneManager.GetActiveScene();
                if (_options == null || _sceneHandle != scene.handle)
                {
                    _sceneHandle = scene.handle;
                    _options = Roll();
                }

                return _options;
            }
        }

        public static bool Apply(PlayerAgent player, ElementType element)
        {
            SkillStateModule skills = player != null ? player.SkillFsm : null;
            SkillData current = skills != null ? skills.GetSkill(SkillSlotId.Dash) : null;
            if (current == null)
                return false;

            if (!ReferenceEquals(current, _clone))
                _source = current;

            SkillData source = _source != null ? _source : current;
            SkillData next = UnityEngine.Object.Instantiate(source);
            next.name = source.name;

            FieldInfo elementField = typeof(SkillData).GetField("element", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo grantsField = typeof(SkillData).GetField("grantsElement", BindingFlags.Instance | BindingFlags.NonPublic);
            if (elementField == null)
            {
                UnityEngine.Object.Destroy(next);
                return false;
            }

            elementField.SetValue(next, element);
            grantsField?.SetValue(next, true);

            FieldInfo colorField = typeof(SkillData).GetField("fxColor", BindingFlags.Instance | BindingFlags.NonPublic);
            if (colorField != null && ElementPalette.Default != null)
            {
                Color color = ElementPalette.Default.ColorOf(element);
                color.a = 1f;
                colorField.SetValue(next, color);
            }

            SkillCooldownState cooldown = skills.GetCooldownState(SkillSlotId.Dash);
            float lockRemaining = skills.GetLockRemaining(SkillSlotId.Dash);
            SkillData previous = _clone;
            _clone = next;
            skills.Equip(SkillSlotId.Dash, next, cooldown, lockRemaining);
            if (previous != null)
                UnityEngine.Object.Destroy(previous);

            return true;
        }

        private static ElementType[] Roll()
        {
            ElementType[] all = (ElementType[])Enum.GetValues(typeof(ElementType));
            for (int i = all.Length - 1; i > 0; i--)
            {
                int swap = UnityEngine.Random.Range(0, i + 1);
                (all[i], all[swap]) = (all[swap], all[i]);
            }

            return new[] { all[0], all[1], all[2] };
        }
    }
}
