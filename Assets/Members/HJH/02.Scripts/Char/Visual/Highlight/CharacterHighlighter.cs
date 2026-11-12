using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.Char.Health;
using Members.JJH._02_Scripts.Agents.Enemies;
using Members.KYR._01.Scripts.Player;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Visual.Highlight
{
    // 표시 대상 하나 (적 또는 플레이어)
    public sealed class HighlightTarget
    {
        public readonly Transform Root;
        public readonly bool IsEnemy;
        public readonly float Radius;   // 발밑 크기 (콜라이더 기준)
        private readonly HealthReader _health;

        public HighlightTarget(Transform root, bool isEnemy)
        {
            Root = root;
            IsEnemy = isEnemy;
            _health = HealthReader.For(root);
            Radius = FootRadius(root);
        }

        public bool IsAlive => Root != null && Root.gameObject.activeInHierarchy && (!_health.IsValid || _health.Current > 0f);

        private static float FootRadius(Transform root)
        {
            float radius = 0f;
            foreach (Collider c in root.GetComponentsInChildren<Collider>())
            {
                if (c.isTrigger || !c.enabled)
                    continue;
                Vector3 e = c.bounds.extents;
                radius = Mathf.Max(radius, Mathf.Max(e.x, e.z));
            }
            return radius > 0.05f ? radius : 0.5f;
        }
    }

    // 표시 방식 하나 (발밑 링 / 림라이트 / 바닥 빛 ...). 새 방식 = 이 클래스를 상속해서 같은 오브젝트에 붙이면 끝
    public abstract class HighlightLayer : MonoBehaviour
    {
        [SerializeField] private bool forEnemies = true;
        [SerializeField] private bool forPlayer;

        private readonly Dictionary<HighlightTarget, bool> _attached = new();
        private readonly List<HighlightTarget> _remove = new();

        public bool Wants(HighlightTarget target) => target.IsEnemy ? forEnemies : forPlayer;

        public void Sync(IReadOnlyList<HighlightTarget> targets)
        {
            foreach (HighlightTarget t in targets)
            {
                bool want = isActiveAndEnabled && Wants(t) && t.IsAlive;
                bool has = _attached.ContainsKey(t);
                if (want && !has)
                {
                    _attached[t] = true;
                    Attach(t);
                }
                else if (!want && has)
                {
                    _attached.Remove(t);
                    Detach(t);
                }
            }

            // 목록에서 사라진(파괴된) 대상 정리
            _remove.Clear();
            foreach (HighlightTarget t in _attached.Keys)
                if (t.Root == null || !Contains(targets, t))
                    _remove.Add(t);
            foreach (HighlightTarget t in _remove)
            {
                _attached.Remove(t);
                Detach(t);
            }

            if (isActiveAndEnabled)
                Tick(_attached.Keys);
        }

        protected virtual void OnDisable()
        {
            foreach (HighlightTarget t in _attached.Keys)
                Detach(t);
            _attached.Clear();
        }

        private static bool Contains(IReadOnlyList<HighlightTarget> list, HighlightTarget t)
        {
            for (int i = 0; i < list.Count; i++)
                if (list[i] == t)
                    return true;
            return false;
        }

        protected abstract void Attach(HighlightTarget target);
        protected abstract void Detach(HighlightTarget target);
        protected virtual void Tick(IEnumerable<HighlightTarget> attached) { }
    }

    // 씬에 하나 두면 적(스폰되는 것 포함)과 플레이어를 찾아서, 같은 오브젝트에 붙은 HighlightLayer 들에 넘겨 줌.
    // 적 프리팹(JJH)은 안 건드림.
    [DefaultExecutionOrder(10050)]
    public sealed class CharacterHighlighter : MonoBehaviour
    {
        [SerializeField, Tooltip("새로 스폰된 적을 찾는 간격 (초)")] private float scanInterval = 0.25f;

        private readonly List<HighlightTarget> _targets = new();
        private readonly Dictionary<Transform, HighlightTarget> _byRoot = new();
        private readonly HashSet<Transform> _seen = new();
        private HighlightLayer[] _layers;
        private float _nextScan;

        private void Awake() => _layers = GetComponents<HighlightLayer>();

        private void LateUpdate()
        {
            if (Time.unscaledTime >= _nextScan)
            {
                _nextScan = Time.unscaledTime + scanInterval;
                Scan();
            }

            foreach (HighlightLayer layer in _layers)
                if (layer != null)
                    layer.Sync(_targets);
        }

        private void Scan()
        {
            _seen.Clear();
            foreach (AbstractEnemy enemy in FindObjectsByType<AbstractEnemy>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                Track(enemy.transform, true);
            foreach (PlayerAgent player in FindObjectsByType<PlayerAgent>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                Track(player.transform, false);

            for (int i = _targets.Count - 1; i >= 0; i--)
            {
                HighlightTarget t = _targets[i];
                if (t.Root != null && _seen.Contains(t.Root))
                    continue;
                if (t.Root != null)
                    _byRoot.Remove(t.Root);
                _targets.RemoveAt(i);
            }
            foreach (Transform dead in new List<Transform>(_byRoot.Keys))
                if (dead == null)
                    _byRoot.Remove(dead);
        }

        private void Track(Transform root, bool isEnemy)
        {
            _seen.Add(root);
            if (_byRoot.ContainsKey(root))
                return;
            var target = new HighlightTarget(root, isEnemy);
            _byRoot[root] = target;
            _targets.Add(target);
        }
    }
}
