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
    // 에디터 전용 자동 점검. Captures/_selftest_on.txt 가 있을 때 플레이를 시작하면 한 번만 돈다 (파일은 바로 지움).
    // 입력 없이 체력바(피격·회복) / 대시 통과 / 강한 처치 조건을 실제 게임 코드로 돌려 보고 Captures/_selftest.txt 에 기록.
    // 캡처 도구(capture.ps1)와 같이 써서 체력바 화면도 찍는다. 게임 빌드에는 안 들어감.
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

        // "highlight:none|ring|rim|light" = 적 가시성 비교 캡처용 (적 멈추고 그 방식만 켬, 다른 점검은 안 함)
        private string _mode = "";

        private IEnumerator HighlightOnly(string which)
        {
            var highlighter = FindFirstObjectByType<Visual.Highlight.CharacterHighlighter>();
            if (highlighter == null) { Log("ERR no CharacterHighlighter"); yield break; }
            highlighter.GetComponent<Visual.Highlight.FootRingLayer>().enabled = which != "none";
            highlighter.GetComponent<Visual.Highlight.RimHighlightLayer>().enabled = which == "rim";
            highlighter.GetComponent<Visual.Highlight.GroundLightLayer>().enabled = which == "light";
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

            // ── 1. 체력바: 피격 35% → 회복 25%
            AbstractEnemy a = enemies[0];
            a.name = "SELFTEST_A";
            HealthReader ra = HealthReader.For(a);
            Log($"A max {ra.Max} cur {ra.Current}");
            yield return new WaitForSeconds(0.5f);   // 3.5
            a.TakeDamage(ra.Max * 0.35f, null);
            player.Health.TakeDamage(player.Health.MaxHealth * 0.3f);
            Log($"A damaged -> {ra.Current}, player -> {player.Health.CurrentHealth}/{player.Health.MaxHealth}");
            yield return new WaitForSeconds(2f);     // 5.5
            ((AgentHealth)a.Health).CurrentHealth += ra.Max * 0.25f;
            player.Heal(player.Health.MaxHealth * 0.2f);
            Log($"A healed -> {ra.Current}, player -> {player.Health.CurrentHealth}");
            yield return new WaitForSeconds(2.5f);   // 8.0

            // ── 2. 대시 통과
            AbstractEnemy d = enemies[1];
            d.name = "SELFTEST_D";
            yield return DashTest(player, d, 2.5f, false, "pass-disabled");
            yield return new WaitForSeconds(0.6f);
            yield return DashTest(player, d, 2.5f, true, "pass");
            yield return new WaitForSeconds(0.6f);
            float dashLen = _lastDashLength;
            yield return DashTest(player, d, dashLen, true, "end-inside");
            yield return new WaitForSeconds(0.8f);

            // ── 3. 강한 처치 조건 (마지막: 슬로모션이 시간을 늦추므로)
            yield return StrongTest(player, enemies[2], 9001, new[] { 0f, 0.3f, 0.3f, 0.3f, 0.3f }, "skill 4x30% (cast total >=70%)", true);
            yield return new WaitForSecondsRealtime(1.5f);
            yield return StrongTest(player, enemies[3], 9002, new[] { 0.85f, 0.3f }, "85% by raw dmg then skill 30% kill (cast total <70%)", false);
            Log("selftest done");
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
            // 첫 값 = 시전 밖 원시 피해 비율 (0 이면 없음)
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
            bool strong = slowTime > 0.2f;   // 강한 처치 = 0.2배 슬로모션 0.6초. 일반 처치는 히트스톱(0배)만 잠깐
            Log($"strong[{label}] alive={enemy.IsAlive} minTimeScale={minScale:F2} slowmo={slowTime:F2}s -> strong={strong} (expected {expectStrong}) {(strong == expectStrong ? "OK" : "FAIL")}");
        }
    }
}
#endif
