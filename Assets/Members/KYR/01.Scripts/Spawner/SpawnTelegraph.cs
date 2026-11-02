using UnityEngine;
using UnityEngine.Rendering;

namespace Members.KYR._01.Scripts.Spawner
{
    public class SpawnTelegraph : MonoBehaviour
    {
        private const int Segments = 40;
        private const float Radius = 0.75f;
        private const float Width = 0.07f;

        private static Material _lineMaterial;

        private LineRenderer _line;

        public static GameObject Create(Vector3 position, Quaternion rotation)
        {
            var marker = new GameObject("Spawn Telegraph");
            marker.transform.SetPositionAndRotation(position, rotation);
            marker.AddComponent<SpawnTelegraph>();
            return marker;
        }

        private void Awake()
        {
            _line = gameObject.AddComponent<LineRenderer>();
            _line.useWorldSpace = false;
            _line.loop = true;
            _line.widthMultiplier = Width;
            _line.positionCount = Segments;
            _line.material = LineMaterial;
            _line.startColor = _line.endColor = new Color(1f, 0.28f, 0.32f, 1f);
            _line.shadowCastingMode = ShadowCastingMode.Off;
            _line.receiveShadows = false;
            _line.numCapVertices = 4;

            for (int i = 0; i < Segments; i++)
            {
                float angle = i / (float)Segments * Mathf.PI * 2f;
                _line.SetPosition(i, new Vector3(Mathf.Cos(angle) * Radius, 0f, Mathf.Sin(angle) * Radius));
            }
        }

        private void Update()
        {
            if (_line == null)
                return;

            float pulse = 1f + Mathf.Sin(Time.time * 8f) * 0.18f;
            _line.widthMultiplier = Width * pulse;
        }

        private static Material LineMaterial
        {
            get
            {
                if (_lineMaterial != null)
                    return _lineMaterial;

                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null)
                    shader = Shader.Find("Sprites/Default");

                _lineMaterial = new Material(shader);
                _lineMaterial.hideFlags = HideFlags.HideAndDontSave;
                Color color = new Color(1f, 0.28f, 0.32f, 1f);
                if (_lineMaterial.HasProperty("_BaseColor"))
                    _lineMaterial.SetColor("_BaseColor", color);
                _lineMaterial.color = color;
                return _lineMaterial;
            }
        }
    }
}
