using System;
using System.Collections;
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
        private static bool _hasChoice;
        private static ElementType _chosen;

        public static bool HasChoice => _hasChoice;
        public static ElementType Chosen => _chosen;

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

            Color color = ElementPalette.Default != null ? ElementPalette.Default.ColorOf(element) : Color.white;
            color.a = 1f;
            next.SetElement(element, color);

            SkillCooldownState cooldown = skills.GetCooldownState(SkillSlotId.Dash);
            float lockRemaining = skills.GetLockRemaining(SkillSlotId.Dash);
            SkillData previous = _clone;
            _clone = next;
            skills.Equip(SkillSlotId.Dash, next, cooldown, lockRemaining);
            if (previous != null)
                UnityEngine.Object.Destroy(previous);

            _chosen = element;
            _hasChoice = true;
            return true;
        }

        public static void ClearChoice()
        {
            _hasChoice = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            _sceneHandle = int.MinValue;
            _options = null;
            _source = null;
            _clone = null;
            _hasChoice = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (UnityEngine.Object.FindAnyObjectByType<DashElementSession>() != null)
                return;

            var host = new GameObject(nameof(DashElementSession));
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<DashElementSession>();
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

    public sealed class DashElementSession : MonoBehaviour
    {
        private const float ReapplyTimeout = 2f;

        private PlayerAgent _player;
        private Coroutine _reapply;

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single)
                return;

            _player = null;
            if (_reapply != null)
                StopCoroutine(_reapply);
            _reapply = StartCoroutine(ReapplyUntilReady());
        }

        private void Update()
        {
            if (!DashElementOffer.HasChoice || _player == null)
                return;

            if (_player.IsAlive)
                return;

            _player = null;
            DashElementOffer.ClearChoice();
            if (_reapply != null)
            {
                StopCoroutine(_reapply);
                _reapply = null;
            }
        }

        private IEnumerator ReapplyUntilReady()
        {
            float deadline = Time.unscaledTime + ReapplyTimeout;
            while (Time.unscaledTime < deadline)
            {
                if (!DashElementOffer.HasChoice)
                    yield break;

                if (_player == null)
                    _player = FindAnyObjectByType<PlayerAgent>();

                if (_player != null && !_player.IsAlive)
                {
                    DashElementOffer.ClearChoice();
                    yield break;
                }

                SkillStateModule skills = _player != null ? _player.SkillFsm : null;
                SkillData dash = skills != null ? skills.GetSkill(SkillSlotId.Dash) : null;
                if (dash != null && (!dash.TryGetElement(out ElementType current) || current != DashElementOffer.Chosen))
                    DashElementOffer.Apply(_player, DashElementOffer.Chosen);

                yield return null;
            }

            _reapply = null;
        }
    }
}
