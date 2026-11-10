#if UNITY_EDITOR
using System.Collections;
using System.Text;
using Assets.Members.HJH._02.Scripts.Char.Character;
using Assets.Members.HJH._02.Scripts.Char.TopDown;
using Members.KYR._01.Scripts.Player;
using RobotWeapons;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Visual
{
    // 에디터 전용 시험 도구: 플레이 중 Temp/GunnerShotProbe.run 파일이 있으면 자동으로 돎.
    // 조준점을 캐릭터 주변 8방향 x 거리 3단계(가까움/중간/멀리)에 놓고 한 발씩 쏴서
    //  - 조준 방향 vs 실제 총알 방향 (각도 차)
    //  - 몸이 바라보는 방향
    //  - 총알 시작점(총구)이 몸에서 얼마나 떨어졌는지
    // 를 Temp/GunnerShotProbe.txt 에 적음. 각도 차가 크면 "BAD" 표시. 끝나면 .run 파일 지움.
    public class GunnerShotProbe : MonoBehaviour
    {
        private const string RunFile = "Temp/GunnerShotProbe.run";
        private const string OutFile = "Temp/GunnerShotProbe.txt";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (!System.IO.File.Exists(RunFile)) return;
            var go = new GameObject("GunnerShotProbe");
            go.AddComponent<GunnerShotProbe>();
        }

        private readonly WeaponVisualTracker _tracker = new();
        private ShotVisual? _last;

        private IEnumerator Start()
        {
            yield return new WaitForSeconds(2f);   // 캐릭터·모델 준비
            PlayerAgent player = FindAnyObjectByType<PlayerAgent>();
            if (player == null) { Write("player 없음"); yield break; }
            _tracker.ShotFired += s => _last = s;

            var sb = new StringBuilder();
            sb.AppendLine("dist angle | aimYaw shotYaw diff | bodyYaw | muzzleOff(right,fwd,up) | result");
            int bad = 0, total = 0;
            float[] dists = { 0.7f, 1.6f, 5f };
            for (int a = 0; a < 8; a++)
            foreach (float d in dists)
            {
                float yaw = a * 45f;
                Vector3 dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                Vector3 p = player.transform.position + dir * d;
                TopDownAimController.ScriptedAimPoint = p;
                yield return new WaitForSeconds(0.15f);
                _last = null;
                float t = 0f;
                while (_last == null && t < 0.6f)
                {
                    _tracker.Track(player);
                    if (player.Weapon != null && player.Weapon.Weapon != null && t == 0f)
                        player.Weapon.Weapon.PrimaryAttack();
                    t += Time.deltaTime;
                    yield return null;
                }
                total++;
                if (_last == null) { sb.AppendLine($"{d,4:0.0} {yaw,5:0} | 발사 안 됨"); bad++; continue; }
                ShotVisual s = _last.Value;
                Vector3 shot = s.End - s.Start; shot.y = 0f;
                float shotYaw = Yaw(shot), diff = Mathf.Abs(Mathf.DeltaAngle(yaw, shotYaw));
                Vector3 off = player.transform.InverseTransformPoint(s.Start);
                bool isBad = diff > 15f;
                if (isBad) bad++;
                sb.AppendLine($"{d,4:0.0} {yaw,5:0} | {yaw,6:0} {shotYaw,6:0} {diff,4:0} | {Yaw(player.transform.forward),6:0} | ({off.x:0.00},{off.z:0.00},{off.y:0.00}) | {(isBad ? "BAD" : "ok")}");
                yield return new WaitForSeconds(0.2f);
            }
            // 스킬: 오른쪽 아래(화면 기준) 쪽을 조준하고 대시 -> 무기 스킬 -> 궁극기 순으로 직접 발동, 프레임 캡처 + 애니 상태 기록
            var fsm = player.SkillFsm;
            var cast = fsm != null ? fsm.GetType().GetMethod("Cast", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance) : null;
            Animator anim = player.GetComponent<CharacterModel>() is { } model ? model.Animator : null;
            sb.AppendLine("\n스킬 | 발동 | 시간별 애니 상태 (Base/Upper)");
            foreach (var slot in new[] { FSM_Skill_Module.SkillSlotId.Dash, FSM_Skill_Module.SkillSlotId.Weapon, FSM_Skill_Module.SkillSlotId.Ultimate })
            {
                TopDownAimController.ScriptedAimPoint = player.transform.position + new Vector3(3f, 0f, -2f);
                yield return new WaitForSeconds(0.3f);
                bool ok = cast != null && (bool)cast.Invoke(fsm, new object[] { slot });
                var line = new StringBuilder($"{slot} | {(ok ? "ok" : "실패")} |");
                float prev = 0f;
                foreach (float at in new[] { 0.03f, 0.12f, 0.25f, 0.4f })
                {
                    yield return new WaitForSeconds(at - prev); prev = at;
                    ScreenCapture.CaptureScreenshot($"Temp/GunnerProbe_{slot}_{at:0.00}.png");
                    line.Append($" {at:0.00}s:{StateName(anim, 0)}/{StateName(anim, 1)}");
                }
                sb.AppendLine(line.ToString());
                yield return new WaitForSeconds(1.2f);
            }

            TopDownAimController.ScriptedAimPoint = null;
            sb.Insert(0, $"총 {total}발, 이상 {bad}발\n");
            Write(sb.ToString());
            System.IO.File.Delete(RunFile);
            Destroy(gameObject);
        }

        private static readonly string[] States = { "Idle", "Run", "Dash", "Attack", "Skill", "WeaponSkill", "Ultimate", "PassiveShot", "Hit", "Dead" };

        private static string StateName(Animator a, int layer)
        {
            if (a == null || layer >= a.layerCount) return "-";
            int h = a.GetCurrentAnimatorStateInfo(layer).shortNameHash;
            foreach (string s in States) if (Animator.StringToHash(s) == h) return s;
            return "?";
        }

        private static float Yaw(Vector3 v) => Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg;

        private static void Write(string text)
        {
            System.IO.File.WriteAllText(OutFile, text);
            Debug.Log("[GunnerShotProbe]\n" + text);
        }
    }
}
#endif
