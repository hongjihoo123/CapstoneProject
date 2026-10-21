using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Assets.Members.HJH._02.Scripts.Char.Visual
{
    // Test-scene helper: plays every library effect one after another at this transform, through the normal Fx calls,
    // so whatever library is active (FxLibraryOverride, F8) is what you see. Toggle key turns the loop on/off.
    public class FxShowcase : MonoBehaviour
    {
        [SerializeField] private float interval = 1.8f;
        [SerializeField] private Color color = new(0.45f, 0.8f, 1f);
        [SerializeField] private Key toggleKey = Key.F7;
        [SerializeField] private bool playing = true;
        [SerializeField, Tooltip("Play at this viewport point on the ground (so the HUD never covers it). Off = at this transform.")]
        private bool followCamera = true;
        [SerializeField] private Vector2 viewportPoint = new(0.5f, 0.62f);

        private (string name, Action<Vector3> play)[] _effects;
        private int _index = -1;
        private float _next;
        private string _current = "";
        private GUIStyle _style;

        private void Awake()
        {
            Vector3 forward = transform.forward;
            _effects = new (string, Action<Vector3>)[]
            {
                ("hit", p => Fx.Hit(p + Vector3.up, forward, color, false)),
                ("hit (big)", p => Fx.Hit(p + Vector3.up, forward, color, true)),
                ("muzzle", p => Fx.Muzzle(p + Vector3.up, forward, color, false)),
                ("explosion (kill, r1.6)", p => Fx.Explosion(p, 1.6f, color, 1.3f)),
                ("explosion (skill, r3)", p => Fx.Explosion(p, 3f, color)),
                ("shockwave", p => Fx.Shockwave(p, color, 2f)),
                ("groundSlam", p => Fx.GroundSlam(p, color, 2.5f)),
                ("characterSwap", p => Fx.CharacterSwap(p, color)),
                ("orb + projectileImpact", p => Orb(p)),
                ("blinkOut", p => Fx.BlinkOut(p, color)),
                ("blinkIn", p => Fx.BlinkIn(p, forward, color)),
                ("charge", p => Charge(p)),
                ("cannonMuzzle", p => Fx.CannonMuzzle(p + Vector3.up, forward, color)),
                ("runeCast", p => Fx.RuneCast(p + Vector3.up, forward, color, 3)),
                ("runeGain", p => Fx.RuneGain(p + Vector3.up, color)),
                ("pull + pullBurst", p => Pull(p)),
                ("lifesteal", p => Fx.Lifesteal(p + Vector3.up + forward * 3f, p + Vector3.up, color)),
                ("frenzyStack", p => Fx.FrenzyStack(p + Vector3.up, color, 2)),
                ("frenzyAura", p => Aura(p)),
                ("slash (unchanged)", p => Fx.Slash(p + Vector3.up, forward, 2f)),
            };
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && toggleKey != Key.None && keyboard[toggleKey].wasPressedThisFrame)
                playing = !playing;

            if (!playing || Time.time < _next)
                return;

            _next = Time.time + interval;
            if (followCamera)
                MoveToCameraPoint();
            _index = (_index + 1) % _effects.Length;
            _current = _effects[_index].name;
            _effects[_index].play(transform.position);
        }

        private void MoveToCameraPoint()
        {
            Camera camera = Camera.main;
            if (camera == null)
                return;

            var ground = new Plane(Vector3.up, new Vector3(0f, transform.position.y, 0f));
            Ray ray = camera.ViewportPointToRay(viewportPoint);
            if (ground.Raycast(ray, out float distance))
                transform.position = ray.GetPoint(distance);
        }

        private void Orb(Vector3 p)
        {
            Vector3 from = p + Vector3.up - transform.forward * 2f;
            FxHandle orb = Fx.Orb(from, color, 0.5f);
            float t = 0f;
            Fx.After(0f, Step);

            void Step()
            {
                t += 0.05f;
                orb.MoveTo(Vector3.Lerp(from, p + Vector3.up, t));
                if (t < 1f)
                    Fx.After(0.03f, Step);
                else
                {
                    orb.Kill();
                    Fx.ProjectileImpact(p + Vector3.up, transform.forward, color, 0.5f, true);
                }
            }
        }

        private void Charge(Vector3 p)
        {
            FxHandle charge = Fx.Charge(p + Vector3.up, color);
            for (int i = 1; i <= 10; i++)
            {
                float value = i / 10f;
                Fx.After(value * 1.2f, () => charge.SetValue(value));
            }

            Fx.After(1.4f, charge.Kill);
        }

        private void Pull(Vector3 p)
        {
            FxHandle pull = Fx.Pull(p, 3f, color);
            Fx.After(1.2f, () =>
            {
                pull.Kill();
                Fx.PullBurst(p, 3f, color);
            });
        }

        private void Aura(Vector3 p)
        {
            FxHandle aura = Fx.FrenzyAura(transform, color, 4);
            Fx.After(1.6f, aura.Kill);
        }

        private void OnGUI()
        {
            _style ??= new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
            string state = playing ? _current : "off";
            GUI.Label(new Rect(12f, Screen.height - 58f, 600f, 28f), $"Showcase: {state}   [{toggleKey}] 켜기/끄기", _style);
        }
    }
}
