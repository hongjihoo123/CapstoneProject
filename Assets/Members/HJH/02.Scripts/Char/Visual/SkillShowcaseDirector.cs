#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using Assets.Members.HJH._02.Scripts.Char.TopDown;
using Members.JJH._02_Scripts.Agents.Enemies;
using Members.KYR._01_Scripts;
using Members.KYR._01_Scripts.Modules;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Assets.Members.HJH._02.Scripts.Char.Visual
{
    // 스킬 시연 자동 녹화 (에디터에서만, 보여주기용. 게임 코드·씬은 안 바꿈)
    //  - 플레이어를 정글 물가로 옮기고, 적 3마리를 마우스가 가리키는 곳에 세움 (AI 끔 = 공격 안 함, 맞는 이펙트용)
    //  - 불·바람 스킬 14개를 Q·E 에 두 개씩 장착 → 바로 시전 → 이펙트가 보일 만큼만 기다리고 다음 (공백 없음)
    //  - 스킬마다 플레이어·적을 제자리로 되돌림. 녹화 중엔 플레이어·적 체력을 계속 가득 채움
    //  - 맞았을 때/위험할 때 빨간 화면 효과(PlayerDamageScreenFx)는 녹화 동안 끔
    //  - 끝나면 Temp/FxBridge/showcase_done.txt 에 결과
    public class SkillShowcaseDirector : MonoBehaviour
    {
        private const string SkillDir = "Assets/Members/HJH/05.Data/Skill/Element/";
        private const float EnemyHealth = 99999f;

        // (스킬, 시전 뒤 기다리는 시간 초) — 이펙트의 핵심 순간만 보이게 짧게
        private static readonly (string skill, float hold)[] Sequence =
        {
            ("Fire/Fire_Fireball", 1.1f), ("Fire/Fire_FlameThrower", 1.7f),
            ("Fire/Fire_FlameDash", 1.0f), ("Fire/Fire_Inferno", 1.4f),
            ("Fire/Fire_Meteor", 1.9f), ("Fire/Fire_FireVeil", 1.6f),
            ("Fire/Fire_Frenzy", 1.3f), ("Wind/Wind_Gust", 1.0f),
            ("Wind/Wind_WindCutter", 1.0f), ("Wind/Wind_Tornado", 1.7f),
            ("Wind/Wind_Leap", 1.2f), ("Wind/Wind_Headwind", 1.6f),
            ("Wind/Wind_Tailwind", 1.3f), ("Wind/Wind_WindVeil", 1.5f),
        };

        private static readonly MethodInfo CastMethod =
            typeof(SkillStateModule).GetMethod("Cast", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(SkillSlotId) }, null);

        private string _path;
        private PlayerAgent _player;
        private PlayerHealth _health;
        private SkillStateModule _module;
        private Vector3 _home;
        private readonly List<(AbstractEnemy enemy, Vector3 spot)> _targets = new();
        private readonly List<AbstractEnemy> _enemies = new();

        public static SkillShowcaseDirector Run(string path)
        {
            var go = new GameObject("[SkillShowcase]") { hideFlags = HideFlags.DontSave };
            var director = go.AddComponent<SkillShowcaseDirector>();
            director._path = path;
            return director;
        }

        private IEnumerator Start()
        {
            _player = FindAnyObjectByType<PlayerAgent>();
            _health = FindAnyObjectByType<PlayerHealth>();
            _module = FindAnyObjectByType<SkillStateModule>();
            if (_player == null || _module == null || CastMethod == null)
            {
                Finish("showcase: 플레이어를 못 찾음");
                yield break;
            }

            // Unity 창이 뒤에 있어도(다른 창을 보고 있어도) 게임이 제 속도로 돌게. 끝나면 되돌림
            _restoreRunInBackground = Application.runInBackground;
            Application.runInBackground = true;

            SetupStage();
            for (int f = 0; f < 5; f++)   // 카메라가 옮긴 플레이어를 따라올 때까지
                yield return null;
            PlaceTargets();
            TopDownAimController.ScriptedAimPoint = _aimPoint;   // 마우스 대신 과녁 쪽 조준 (사람 조작 없음)
            ResetStage();
            yield return new WaitForSeconds(0.5f);
            GameplayCapture capture = GameplayCapture.Begin(_path);
            yield return new WaitForSeconds(0.3f);

            for (int i = 0; i < Sequence.Length; i++)
            {
                bool q = i % 2 == 0;
                var data = AssetDatabase.LoadAssetAtPath<SkillData>(SkillDir + Sequence[i].skill + ".asset");
                if (data == null)
                    continue;
                if (q)   // Q·E 를 같이 바꿔 UI 에 지금 두 스킬이 보이게
                {
                    var next = i + 1 < Sequence.Length ? AssetDatabase.LoadAssetAtPath<SkillData>(SkillDir + Sequence[i + 1].skill + ".asset") : null;
                    _module.Equip(SkillSlotId.Basic1, data);
                    _module.Equip(SkillSlotId.Basic2, next);
                }

                ResetStage();
                Cast(q ? SkillSlotId.Basic1 : SkillSlotId.Basic2);
                yield return new WaitForSeconds(Sequence[i].hold);

                if (i == 5 || i == Sequence.Length - 1)   // 불 끝, 바람 끝에 궁극기
                {
                    ResetStage();
                    Cast(SkillSlotId.Ultimate);
                    yield return new WaitForSeconds(1.6f);
                }
            }

            string result = $"showcase: {capture.Frames} 프레임 ({capture.Frames / GameplayCapture.FramesPerSecond:0.0}초) -> {capture.FilePath}";
            Destroy(capture.gameObject);
            Finish(result);
        }

        // 녹화 중엔 아무도 안 죽게
        private void Update()
        {
            if (_health != null && _health.CurrentHealth < _health.MaxHealth)
                _health.InitHealth(_health.MaxHealth);
            foreach (AbstractEnemy enemy in _enemies)
                if (enemy != null && enemy.Health != null && enemy.Health.CurrentHealth < EnemyHealth * 0.5f)
                    enemy.Health.InitHealth(EnemyHealth);
        }

        private void Cast(SkillSlotId slot)
        {
            _module.ForceIdle();
            CastMethod.Invoke(_module, new object[] { slot });
        }

        // 정글 물가로 옮기고, 마우스가 가리키는 곳에 적 3마리
        private void SetupStage()
        {
            Renderer water = FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .FirstOrDefault(r => r.sharedMaterial != null && r.sharedMaterial.shader.name.Contains("ShallowWater"));
            _home = Ground(water != null ? water.bounds.center + new Vector3(-3.5f, 0f, -1f) : _player.transform.position);
            Place(_player.transform, _home);

            // 빨간 화면 효과 끄기
            foreach (Volume v in FindObjectsByType<Volume>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (v.name.StartsWith("PlayerHitVolume") || v.name.StartsWith("PlayerDangerVolume"))
                    v.enabled = false;
            foreach (PlayerDamageScreenFx fx in FindObjectsByType<PlayerDamageScreenFx>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                fx.enabled = false;

            // 적: AI 끄고 (공격·이동 안 함) 앞쪽 3마리를 과녁으로
            _enemies.AddRange(FindObjectsByType<AbstractEnemy>(FindObjectsInactive.Exclude, FindObjectsSortMode.None));
            foreach (AbstractEnemy enemy in _enemies)
                foreach (Behaviour b in enemy.GetComponentsInChildren<Behaviour>())
                    if (b.GetType().Name is "BehaviorGraphAgent" or "NavMeshAgent")
                        b.enabled = false;
        }

        // 마우스가 가리키는 곳에 적 3마리 (과녁)
        private void PlaceTargets()
        {
            // 화면 오른쪽 조금 위 (조준 마우스를 그쪽에 둠). 카메라가 막 움직인 직후라 마우스 광선 대신 카메라 방향으로
            Camera cam = Camera.main;
            Vector3 right = cam != null ? Vector3.ProjectOnPlane(cam.transform.right, Vector3.up).normalized : Vector3.right;
            Vector3 up = Vector3.Cross(right, Vector3.up);   // 화면 위쪽 (탑뷰에서 앞)
            Vector3 forward = (right + up * 0.2f).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, forward);
            Vector3 center = _home + forward * 5f;
            _aimPoint = center;
            for (int i = 0; i < Mathf.Min(3, _enemies.Count); i++)
                _targets.Add((_enemies[i], Ground(center + side * ((i - 1) * 1.4f) + forward * (i == 1 ? 0.6f : 0f))));
        }

        // 스킬마다 제자리로
        private void ResetStage()
        {
            Place(_player.transform, _home);
            foreach ((AbstractEnemy enemy, Vector3 spot) in _targets)
                if (enemy != null)
                    Place(enemy.transform, spot);
        }

        private static void Place(Transform t, Vector3 position)
        {
            var controller = t.GetComponent<CharacterController>();
            if (controller != null)
                controller.enabled = false;
            t.position = position + Vector3.up * 0.05f;
            if (t.TryGetComponent(out Rigidbody body) && !body.isKinematic)
                body.linearVelocity = Vector3.zero;
            if (controller != null)
                controller.enabled = true;
        }

        // 바닥 높이 (캐릭터 몸은 건너뜀)
        private static Vector3 Ground(Vector3 p)
        {
            RaycastHit[] hits = Physics.RaycastAll(p + Vector3.up * 40f, Vector3.down, 80f, ~0, QueryTriggerInteraction.Ignore);
            foreach (RaycastHit hit in hits.OrderBy(h => h.distance))
            {
                if (hit.collider.GetComponentInParent<PlayerAgent>() != null || hit.collider.GetComponentInParent<AbstractEnemy>() != null)
                    continue;
                return hit.point;
            }
            return p;
        }

        private bool _restoreRunInBackground;
        private Vector3 _aimPoint;
        private bool _restored;

        // 중간에 플레이를 꺼도 원래대로
        private void OnDestroy() => Restore();

        private void Restore()
        {
            if (_restored)
                return;
            _restored = true;
            TopDownAimController.ScriptedAimPoint = null;
            Application.runInBackground = _restoreRunInBackground;
        }

        private void Finish(string message)
        {
            Restore();
            Debug.Log("[Showcase] " + message);
            Directory.CreateDirectory("Temp/FxBridge");
            File.WriteAllText("Temp/FxBridge/showcase_done.txt", message);
            Destroy(gameObject);
        }
    }
}
#endif
