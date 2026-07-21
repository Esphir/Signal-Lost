using UnityEngine;

namespace Signal.Combat.Telegraphs
{
    /// <summary>
    /// Everything an <see cref="AoeTelegraph"/> needs for one warning. Built by the caller from
    /// its own config (projectile SO, slam SO, …) so the telegraph stays enemy-agnostic.
    /// </summary>
    public struct AoeTelegraphSettings
    {
        public float Radius;             // the REAL damage radius — ring matches it exactly at scale 1
        public Color Color;
        public float ScaleMultiplier;    // 1 = exact damage radius; scale up only for readability
        public float PulseSpeed;         // pulses per second, 0 = none
        public float WarningDuration;    // time until impact — drives the heat-up tint
        public GameObject AppearVfx;     // optional hook, spawned at the telegraph position
    }

    /// <summary>
    /// Reusable ground-marker warning for any AoE attack: an outer ring sized exactly to the
    /// damage radius, a filled center marking the impact point, optional pulse, and a hotter tint
    /// over the last quarter of the warning. Used by the Lobber's projectile landing indicator and
    /// the Plummeter's slam telegraph; future enemies just call <see cref="Create"/> + <see cref="Show"/>.
    ///
    /// Instances are owned 1:1 by their attacker/projectile (created once, shown/hidden per use),
    /// so telegraphs are pooled implicitly and never orphaned. No allocations after construction.
    /// </summary>
    public class AoeTelegraph : MonoBehaviour
    {
        private const int RingSegments = 40;
        private const float RingWidth = 0.08f;      // relative to a unit-radius circle
        private const float CenterDotScale = 0.35f; // relative to a unit-radius circle
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private LineRenderer _ring;
        private Renderer _centerRenderer;
        private MaterialPropertyBlock _tintBlock;

        private AoeTelegraphSettings _settings;
        private float _shownAt;

        /// <summary>
        /// Creates a telegraph instance from a prefab (an AoeTelegraph is added if missing), or
        /// builds the built-in procedural ring when no prefab is supplied. Returned inactive.
        /// </summary>
        public static AoeTelegraph Create(GameObject prefab)
        {
            GameObject go = prefab != null ? Instantiate(prefab) : new GameObject("AoeTelegraph");
            var telegraph = go.GetComponent<AoeTelegraph>();
            if (telegraph == null) telegraph = go.AddComponent<AoeTelegraph>();
            if (prefab == null) telegraph.BuildProceduralVisual();
            go.SetActive(false);
            return telegraph;
        }

        /// <summary>Places and shows the warning at a world position and fires the appear VFX/SFX hooks.</summary>
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

            // Heat up over the last quarter of the warning as a "get out" cue.
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

        /// <summary>Unit-radius ring + center quad, scaled to the damage radius via the transform.</summary>
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
