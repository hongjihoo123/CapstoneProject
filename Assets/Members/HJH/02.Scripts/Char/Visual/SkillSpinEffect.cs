using System.Collections.Generic;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Visual
{
    public class SkillSpinEffect : MonoBehaviour
    {
        private static readonly string[] SlashSystemNames = { "slash_alp", "slash_add" };

        [SerializeField] private float rotationSpeed = 1440f;
        [SerializeField] private float height = 0.9f;
        [SerializeField] private int blades = 2;
        [SerializeField] private float loopDuration = 1f;
        [SerializeField] private float lifetimeOverlap = 1.15f;

        private readonly List<GameObject> _instances = new();
        private readonly List<ParticleSystem> _systems = new();
        private Transform _orbit;
        private GameObject _builtFrom;
        private float _builtRadius;
        private bool _playing;

        private void Update()
        {
            if (_playing)
                _orbit.Rotate(0f, rotationSpeed * Time.deltaTime, 0f, Space.Self);
        }

        public void Play(GameObject prefab, float radius)
        {
            if (prefab == null)
                return;

            if (_builtFrom != prefab || !Mathf.Approximately(_builtRadius, radius))
                Rebuild(prefab, radius);

            foreach (GameObject instance in _instances)
                instance.SetActive(true);

            foreach (ParticleSystem system in _systems)
            {
                system.Clear(true);
                system.Play(true);
            }

            _playing = true;
        }

        public void Stop()
        {
            _playing = false;
            foreach (ParticleSystem system in _systems)
                system.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        private void Rebuild(GameObject prefab, float radius)
        {
            foreach (GameObject instance in _instances)
                Destroy(instance);

            _instances.Clear();
            _systems.Clear();

            if (_orbit == null)
            {
                _orbit = new GameObject("SpinEffectOrbit").transform;
                _orbit.SetParent(transform, false);
                _orbit.localPosition = new Vector3(0f, height, 0f);
            }

            for (int i = 0; i < Mathf.Max(1, blades); i++)
            {
                var holder = new GameObject($"Blade{i}").transform;
                holder.SetParent(_orbit, false);
                holder.localRotation = Quaternion.Euler(0f, i * 360f / Mathf.Max(1, blades), 0f);

                GameObject instance = Instantiate(prefab, holder);
                ConfigureSlashSystems(instance, radius);
                holder.gameObject.SetActive(false);
                _instances.Add(holder.gameObject);
            }

            _builtFrom = prefab;
            _builtRadius = radius;
        }

        private void ConfigureSlashSystems(GameObject instance, float radius)
        {
            float baseRadius = 0f;

            foreach (ParticleSystem system in instance.GetComponentsInChildren<ParticleSystem>(true))
            {
                system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

                if (System.Array.IndexOf(SlashSystemNames, system.name) < 0)
                {
                    DisableUnusedSystem(system);
                    continue;
                }

                ParticleSystem.MainModule main = system.main;
                main.loop = true;
                main.playOnAwake = false;
                main.duration = loopDuration;
                main.startLifetime = lifetimeOverlap;

                ParticleSystem.RotationOverLifetimeModule rotation = system.rotationOverLifetime;
                rotation.enabled = false;

                var particleRenderer = system.GetComponent<ParticleSystemRenderer>();
                if (particleRenderer.mesh != null && baseRadius <= 0f)
                {
                    Vector3 extents = particleRenderer.mesh.bounds.extents;
                    baseRadius = Mathf.Max(extents.x, extents.y) * main.startSize.constant * system.transform.lossyScale.x;
                }

                _systems.Add(system);
            }

            if (baseRadius > 0.0001f)
                instance.transform.localScale *= radius / baseRadius;
        }

        // slash_alp/slash_add are children of "fx" and the prefab root, so those parents must stay active.
        private static void DisableUnusedSystem(ParticleSystem system)
        {
            bool isContainer = system.transform.childCount > 0;
            if (isContainer)
            {
                ParticleSystem.EmissionModule emission = system.emission;
                emission.enabled = false;
                return;
            }

            system.gameObject.SetActive(false);
        }
    }
}
