#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Text;
using Assets.Members.HJH._02.Scripts.Char.Health;
using Assets.Members.HJH._02.Scripts.Char.Movement;
using Members.JJH._02_Scripts.Agents.Enemies;
using Members.JJH._02_Scripts.Agents.Modules;
using Members.KYR._01.Scripts.Player;
using RobotWeapons;
using Unity.Behavior;
using UnityEngine;
using UnityEngine.AI;

namespace Assets.Members.HJH._02.Scripts.Char.Debugging
{
    public sealed class PlaySelfTest : MonoBehaviour
    {
        private const string Flag = "Captures/_selftest_on.txt";
        private const string LogPath = "Captures/_selftest.txt";
        private readonly StringBuilder _log = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (!File.Exists(Flag))
                return;
            string mode = File.ReadAllText(Flag).Trim();
            File.Delete(Flag);
            new GameObject("PlaySelfTest").AddComponent<PlaySelfTest>()._mode = mode;
        }

        private string _mode = "";

        private IEnumerator HighlightOnly(string which)
        {
            var highlighter = FindFirstObjectByType<Visual.Highlight.CharacterHighlighter>();
            if (highlighter == null) { Log("ERR no CharacterHighlighter"); yield break; }
            highlighter.GetComponent<Visual.Highlight.FootRingLayer>().enabled = which != "none";
            highlighter.GetComponent<Visual.Highlight.RimHighlightLayer>().enabled = which == "rim";
            highlighter.GetComponent<Visual.Highlight.GroundLightLayer>().enabled = which == "light";
            var key = highlighter.GetComponent<Visual.Highlight.CharacterLightLayer>();
            if (key != null) key.enabled = which == "charlight";
            Log("highlight mode " + which);
            yield return new WaitForSeconds(2.5f);
            foreach (AbstractEnemy e in FindObjectsByType<AbstractEnemy>(FindObjectsSortMode.None))
                Freeze(e);
            PlayerAgent player = FindFirstObjectByType<PlayerAgent>();
            AbstractEnemy[] enemies = FindObjectsByType<AbstractEnemy>(FindObjectsSortMode.None);
            if (player != null && enemies.Length > 0)
            {
                System.Array.Sort(enemies, (a, b) => Dist(a.transform, player.transform).CompareTo(Dist(b.transform, player.transform)));
                enemies[0].name = "SELFTEST_A";
            }
        }

        private void Log(string line)
        {
            _log.AppendLine($"[{Time.time:F2}] {line}");
            File.WriteAllText(LogPath, _log.ToString());
        }

        private IEnumerator Start()
        {
            Log("selftest start " + _mode);
            if (_mode == "push" || _mode == "push-off")
            {
                yield return PushTest();
                yield break;
            }
            if (_mode == "death")
            {
                yield return new WaitForSeconds(3f);
                PlayerAgent victim = FindFirstObjectByType<PlayerAgent>();
                if (victim != null) victim.TakeDamage(victim.Health.MaxHealth * 10f, null);
                Log("player killed");
                yield break;
            }
            if (_mode == "cc")
            {
                yield return CcTest();
                yield break;
            }
            if (_mode.StartsWith("highlight:"))
            {
                yield return HighlightOnly(_mode.Substring("highlight:".Length));
                yield break;
            }
            yield return new WaitForSeconds(3f);

            PlayerAgent player = FindFirstObjectByType<PlayerAgent>();
            if (player == null) { Log("ERR no player"); yield break; }

            AbstractEnemy[] enemies = FindObjectsByType<AbstractEnemy>(FindObjectsSortMode.None);
            System.Array.Sort(enemies, (a, b) => Dist(a.transform, player.transform).CompareTo(Dist(b.transform, player.transform)));
            Log($"enemies {enemies.Length}");
            if (enemies.Length < 4) { Log("ERR need 4 enemies"); yield break; }
            foreach (AbstractEnemy e in enemies)
                Freeze(e);

            AbstractEnemy a = enemies[0];
            a.name = "SELFTEST_A";
            HealthReader ra = HealthReader.For(a);
            Log($"A max {ra.Max} cur {ra.Current}");
            yield return new WaitForSeconds(0.5f);
            a.TakeDamage(ra.Max * 0.35f, null);
            player.TakeDamage(player.Health.MaxHealth * 0.3f, null);
            Log($"A damaged -> {ra.Current}, player -> {player.Health.CurrentHealth}/{player.Health.MaxHealth}");
            yield return new WaitForSeconds(2f);
            ((AgentHealth)a.Health).CurrentHealth += ra.Max * 0.25f;
            player.Heal(player.Health.MaxHealth * 0.2f);
            Log($"A healed -> {ra.Current}, player -> {player.Health.CurrentHealth}");
            yield return new WaitForSeconds(2.5f);

            AbstractEnemy d = enemies[1];
            d.name = "SELFTEST_D";
            yield return DashTest(player, d, 2.5f, false, "pass-disabled");
            yield return new WaitForSeconds(0.6f);
            yield return DashTest(player, d, 2.5f, true, "pass");
            yield return new WaitForSeconds(0.6f);
            float dashLen = _lastDashLength;
            yield return DashTest(player, d, dashLen, true, "end-inside");
            yield return new WaitForSeconds(0.8f);

            yield return StrongTest(player, enemies[2], 9001, new[] { 0f, 0.3f, 0.3f, 0.3f, 0.3f }, "skill 4x30% (cast total >=70%)", true);
            yield return new WaitForSecondsRealtime(1.5f);
            yield return StrongTest(player, enemies[3], 9002, new[] { 0.85f, 0.3f }, "85% by raw dmg then skill 30% kill (cast total <70%)", false);
            Log("selftest done");
        }

        private IEnumerator CcTest()
        {
            yield return new WaitForSeconds(2f);
            AbstractEnemy[] enemies = FindObjectsByType<AbstractEnemy>(FindObjectsSortMode.None);
            Log($"enemies {enemies.Length}");
            if (enemies.Length < 2) { Log("ERR need 2 enemies"); yield break; }

            AbstractEnemy target = null;
            float until = Time.time + 8f;
            while (target == null && Time.time < until)
            {
                foreach (AbstractEnemy e in FindObjectsByType<AbstractEnemy>(FindObjectsSortMode.None))
                    if (e.IsAlive && e.SkillModule != null && e.SkillModule.IsSkillUsing()) { target = e; break; }
                if (target == null) yield return null;
            }
            bool wasUsingSkill = target != null;
            if (target == null) target = enemies[0];
            Log($"target {target.name} usingSkill={wasUsingSkill}");

            var bt = target.GetComponent<BehaviorGraphAgent>();
            CrowdControl.Of(target).ApplyStun(1.2f);
            yield return null;
            yield return null;
            yield return new WaitForSeconds(0.1f);
            var anim = target.Renderer?.Animator;
            bool inStun = anim != null && anim.GetCurrentAnimatorStateInfo(0).shortNameHash == Animator.StringToHash("STUNNED")
                          || anim != null && anim.GetNextAnimatorStateInfo(0).shortNameHash == Animator.StringToHash("STUNNED");
            Log($"diag: receiver={target.GetComponent<CrowdControlReceiver>() != null} adapter={target.GetComponent<Assets.Members.HJH._02.Scripts.Weapon.Runtime.EnemyCcInterrupt>() != null} interrupted={(target.GetComponent<CrowdControlReceiver>()?.IsInterrupted)} btOnSelf={bt != null} btInChildren={target.GetComponentInChildren<BehaviorGraphAgent>() != null}");
            Log($"stun: btEnabled={bt != null && bt.enabled} usingSkill={target.SkillModule.IsSkillUsing()} stunAnim={inStun} -> {(bt != null && !bt.enabled && !target.SkillModule.IsSkillUsing() ? "OK" : "FAIL")}");
            yield return new WaitForSeconds(1.6f);
            Log($"after stun: btEnabled={bt != null && bt.enabled} -> {(bt != null && bt.enabled ? "OK" : "FAIL")}");

            AbstractEnemy other = enemies[0] == target ? enemies[1] : enemies[0];
            var bt2 = other.GetComponent<BehaviorGraphAgent>();
            if (CrowdControl.Of(other) is CrowdControlReceiver receiver)
            {
                receiver.ApplyKnockback(Vector3.forward, 2f);
                receiver.Interrupt(0.35f);
            }
            yield return null;
            yield return null;
            Log($"knockback: btEnabled={bt2 != null && bt2.enabled} -> {(bt2 != null && !bt2.enabled ? "OK" : "FAIL")}");
            yield return new WaitForSeconds(0.8f);
            Log($"after knockback: btEnabled={bt2 != null && bt2.enabled} -> {(bt2 != null && bt2.enabled ? "OK" : "FAIL")}");
            Log("cc test done");
        }

        private Vector3 _lastPushPos;
        private int _pushLogs;

        private IEnumerator PushTest()
        {
            yield return new WaitForSeconds(1f);
            PlayerAgent player = FindFirstObjectByType<PlayerAgent>();
            if (player == null) { Log("ERR no player"); yield break; }
            Vector3 start = player.transform.position;
            _lastPushPos = start;
            float maxDrift = 0f, minGap = float.MaxValue;
            float until = Time.time + 8f;
            while (Time.time < until)
            {
                player.Heal(9999f);
                if (_mode == "push-off")
                    foreach (var s in FindObjectsByType<Assets.Members.HJH._02.Scripts.Weapon.Runtime.EnemyBodySpacing>(FindObjectsSortMode.None)) s.enabled = false;
                Vector3 d = player.transform.position - start; d.y = 0f;
                maxDrift = Mathf.Max(maxDrift, d.magnitude);
                Vector3 step = player.transform.position - _lastPushPos; step.y = 0f;
                if (step.magnitude > 0.01f && _pushLogs < 12)
                {
                    _pushLogs++;
                    CharacterController cc = player.Mover.Body;
                    Vector3 c = player.transform.position + cc.center;
                    float half = Mathf.Max(0f, cc.height * 0.5f - cc.radius);
                    var names = new System.Collections.Generic.List<string>();
                    foreach (Collider hit in Physics.OverlapCapsule(c - Vector3.up * half, c + Vector3.up * half, cc.radius + 0.02f, ~0, QueryTriggerInteraction.Ignore))
                        if (hit != cc && !hit.transform.IsChildOf(player.transform))
                            names.Add($"{hit.transform.root.name}/{hit.name}({LayerMask.LayerToName(hit.gameObject.layer)},{hit.GetType().Name})");
                    Log($"  pushed {step.magnitude:F3}m dir {step.normalized}, touching: {string.Join(", ", names)}");
                }
                _lastPushPos = player.transform.position;
                foreach (AbstractEnemy e in FindObjectsByType<AbstractEnemy>(FindObjectsSortMode.None))
                {
                    if (!e.IsAlive) continue;
                    Vector3 o = e.transform.position - player.transform.position; o.y = 0f;
                    minGap = Mathf.Min(minGap, o.magnitude);
                }
                yield return null;
            }
            Log($"push: player drift max {maxDrift:F2}m (0 = 안 밀림), closest enemy center {minGap:F2}m -> {(maxDrift < 0.15f ? "OK" : "FAIL")}");

            AbstractEnemy[] enemies = FindObjectsByType<AbstractEnemy>(FindObjectsSortMode.None);
            System.Array.Sort(enemies, (a, b) => Dist(a.transform, player.transform).CompareTo(Dist(b.transform, player.transform)));
            AbstractEnemy victim = null;
            foreach (AbstractEnemy e in enemies) if (e.IsAlive) { victim = e; break; }
            if (victim == null) { Log("ERR no enemy"); yield break; }
            CrowdControl.Of(victim);
            if (CrowdControl.Of(victim) is CrowdControlReceiver r) r.ApplyFlinch(0.1f);
            float before = Dist(victim.transform, player.transform);
            victim.TakeDamage(99999f, player.gameObject);
            Vector3 corpseStart = victim.transform.position;
            yield return new WaitForSeconds(1.0f);
            if (victim == null) { Log("corpse: destroyed within 1s (OK)"); Log("push test done"); yield break; }
            Vector3 moved = victim.transform.position - corpseStart; moved.y = 0f;
            var box = victim.GetComponent<Collider>();
            Log($"corpse: was {before:F2}m away, slid {moved.magnitude:F2}m, body collider enabled={box != null && box.enabled} -> {(moved.magnitude < 0.2f && (box == null || !box.enabled) ? "OK" : "FAIL")}");
            Log("push test done");
        }

        private static float Dist(Transform a, Transform b) => Vector3.Distance(a.position, b.position);

        private static void Freeze(AbstractEnemy e)
        {
            if (e.TryGetComponent(out BehaviorGraphAgent bt)) bt.enabled = false;
            NavMeshAgent agent = e.GetComponentInChildren<NavMeshAgent>();
            if (agent != null && agent.isOnNavMesh) { agent.isStopped = true; agent.velocity = Vector3.zero; }
        }

        private float _lastDashLength;

        private IEnumerator DashTest(PlayerAgent player, AbstractEnemy enemy, float startDistance, bool passThrough, string label)
        {
            DashPassThrough pass = player.GetComponent<DashPassThrough>();
            if (pass != null) pass.enabled = passThrough;

            Vector3 enemyPos = enemy.transform.position;
            Vector3 toEnemy = enemyPos - player.transform.position; toEnemy.y = 0f;
            Vector3 dir = toEnemy.sqrMagnitude > 0.01f ? toEnemy.normalized : Vector3.forward;
            Vector3 start = enemyPos - dir * startDistance;
            start.y = player.transform.position.y;
            player.Mover.Teleport(start);
            yield return null;
            yield return null;
            start = player.transform.position;
            Vector3 enemyBefore = enemy.transform.position;

            player.Mover.Dash(dir, 16f, 0.3f);
            float minDist = float.MaxValue;
            float t = 0f;
            while (t < 0.1f || player.Mover.IsDashing)
            {
                t += Time.deltaTime;
                Vector3 p = player.transform.position - enemyPos; p.y = 0f;
                minDist = Mathf.Min(minDist, p.magnitude);
                if (t > 2f) break;
                yield return null;
            }
            yield return null;
            yield return null;

            Vector3 end = player.transform.position;
            Vector3 travel = end - start; travel.y = 0f;
            if (label == "pass") _lastDashLength = travel.magnitude;
            float along = Vector3.Dot(end - enemyPos, dir);
            CharacterController cc = player.Mover.Body;
            Vector3 c = end + cc.center;
            float half = Mathf.Max(0f, cc.height * 0.5f - cc.radius);
            bool overlap = Physics.CheckCapsule(c - Vector3.up * (half - cc.stepOffset), c + Vector3.up * half, cc.radius, LayerMask.GetMask("Enemy"), QueryTriggerInteraction.Ignore);
            Vector3 shove = enemy.transform.position - enemyBefore; shove.y = 0f;
            Log($"dash[{label}] start {startDistance:F2}m before enemy, travelled {travel.magnitude:F2}m, min dist to enemy center {minDist:F2}, " +
                $"end {(along > 0 ? "PAST" : "BEFORE")} enemy ({along:F2}m), overlap at end={overlap}, enemy shoved {shove.magnitude:F2}m");
            if (pass != null) pass.enabled = true;
        }

        private IEnumerator StrongTest(PlayerAgent player, AbstractEnemy enemy, int castId, float[] hits, string label, bool expectStrong)
        {
            HealthReader r = HealthReader.For(enemy);
            float max = r.Max;
            if (hits[0] > 0f) enemy.TakeDamage(max * hits[0], null);
            float slowTime = 0f; float minScale = Time.timeScale;
            for (int i = 1; i < hits.Length && enemy.IsAlive; i++)
            {
                HitSource.Push(HitSourceKind.Skill, castId);
                try { player.ApplyDamageTo(enemy, max * hits[i]); }
                finally { HitSource.Pop(); }
                yield return null;
                minScale = Mathf.Min(minScale, Time.timeScale);
                if (Time.timeScale > 0.1f && Time.timeScale < 0.6f) slowTime += Time.unscaledDeltaTime;
            }
            float until = Time.realtimeSinceStartup + 0.8f;
            while (Time.realtimeSinceStartup < until)
            {
                minScale = Mathf.Min(minScale, Time.timeScale);
                if (Time.timeScale > 0.1f && Time.timeScale < 0.6f) slowTime += Time.unscaledDeltaTime;
                yield return null;
            }
            bool strong = slowTime > 0.2f;
            Log($"strong[{label}] alive={enemy.IsAlive} minTimeScale={minScale:F2} slowmo={slowTime:F2}s -> strong={strong} (expected {expectStrong}) {(strong == expectStrong ? "OK" : "FAIL")}");
        }
    }
}
#endif
