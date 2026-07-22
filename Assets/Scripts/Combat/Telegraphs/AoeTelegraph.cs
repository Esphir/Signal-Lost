// Everything an AoeTelegraph needs for one warning.
using UnityEngine;

namespace Signal.Combat.Telegraphs
{
    public struct AoeTelegraphSettings
    {
        public float Radius;
        public Color Color;
        public float ScaleMultiplier;
        public float PulseSpeed;
        public float WarningDuration;
        public GameObject AppearVfx;
    }

    public class AoeTelegraph : MonoBehaviour
    {
        private const int RingSegments = 40;
        private const float RingWidth = 0.08f;
        private const float CenterDotScale = 0.35f;
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private LineRenderer _ring;
        private Renderer _centerRenderer;
        private MaterialPropertyBlock _tintBlock;

        private AoeTelegraphSettings _settings;
        private float _shownAt;

        public static AoeTelegraph Create(GameObject prefab)
        {
            GameObject go = prefab != null ? Instantiate(prefab) : new GameObject("AoeTelegraph");
            var telegraph = go.GetComponent<AoeTelegraph>();
            if (telegraph == null) telegraph = go.AddComponent<AoeTelegraph>();
            if (prefab == null) telegraph.BuildProceduralVisual();
            go.SetActive(false);
            return telegraph;
        }

        public void Show(Vector3 position, in AoeTelegraphSettings settings)
        {
            _settings = settings;
            _settings.Radius = Mathf.Max(0.1f, settings.Radius);
            _settings.ScaleMultiplier = Mathf.Max(0.05f, settings.ScaleMultiplier);
            _settings.WarningDuration = Mathf.Max(0.05f, settings.WarningDuration);
            _shownAt = Time.time;

            transform.SetPositionAndRotation(position + Vector3.up * 0.03f, Quaternion.identity);
            ApplyScale(1f);
            ApplyColor(_settings.Color);
            gameObject.SetActive(true);

            if (_settings.AppearVfx != null)
                Instantiate(_settings.AppearVfx, position, Quaternion.identity);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void Update()
        {
            float elapsed = Time.time - _shownAt;

            float pulse = _settings.PulseSpeed > 0f
                ? 1f + 0.06f * Mathf.Sin(elapsed * _settings.PulseSpeed * 2f * Mathf.PI)
                : 1f;
            ApplyScale(pulse);

            float progress = Mathf.Clamp01(elapsed / _settings.WarningDuration);
            if (progress > 0.75f)
                ApplyColor(Color.Lerp(_settings.Color, Color.red, (progress - 0.75f) / 0.25f));
        }

        private void ApplyScale(float pulse)
            => transform.localScale = Vector3.one * (_settings.Radius * _settings.ScaleMultiplier * pulse);

        private void ApplyColor(Color color)
        {
            if (_ring != null)
            {
                _ring.startColor = color;
                _ring.endColor = color;
            }

            if (_centerRenderer != null)
            {
                _tintBlock ??= new MaterialPropertyBlock();
                _tintBlock.SetColor(ColorId, color);
                _centerRenderer.SetPropertyBlock(_tintBlock);
            }
        }

        private void BuildProceduralVisual()
        {
            var material = new Material(Shader.Find("Sprites/Default"));

            _ring = gameObject.AddComponent<LineRenderer>();
            _ring.useWorldSpace = false;
            _ring.loop = true;
            _ring.positionCount = RingSegments;
            _ring.widthMultiplier = RingWidth;
            _ring.material = material;
            _ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _ring.receiveShadows = false;

            for (int i = 0; i < RingSegments; i++)
            {
                float angle = i / (float)RingSegments * 2f * Mathf.PI;
                _ring.SetPosition(i, new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)));
            }

            GameObject center = GameObject.CreatePrimitive(PrimitiveType.Quad);
            center.name = "ImpactCenter";
            Destroy(center.GetComponent<Collider>());
            center.transform.SetParent(transform, false);
            center.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            center.transform.localScale = Vector3.one * CenterDotScale;

            _centerRenderer = center.GetComponent<MeshRenderer>();
            _centerRenderer.sharedMaterial = material;
            _centerRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _centerRenderer.receiveShadows = false;
        }
    }
}
