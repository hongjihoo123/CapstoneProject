using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Visual
{
    public class FxReach : MonoBehaviour, IFxDriver
    {
        public enum Mode { Inward, Toward }

        [SerializeField] private Mode mode = Mode.Inward;
        [SerializeField] private ParticleSystem[] systems;

        public bool IsPlaying => false;

        public void Play(in FxParams parameters)
        {
            float distance = mode == Mode.Inward ? parameters.Radius : Vector3.Distance(parameters.Start, parameters.End);
            foreach (ParticleSystem system in systems)
            {
                ParticleSystem.MainModule main = system.main;
                float life = Mathf.Max(0.01f, main.startLifetime.constantMax);

                if (mode == Mode.Inward)
                {
                    ParticleSystem.ShapeModule shape = system.shape;
                    shape.radius = distance;
                    main.startSpeed = -distance / life;
                }
                else
                {
                    main.startSpeed = distance / life;
                }
            }
        }

        public void Stop() { }
    }
}
