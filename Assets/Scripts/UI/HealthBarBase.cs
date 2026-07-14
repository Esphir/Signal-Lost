using Signal.Combat.Health;
using UnityEngine;
using UnityEngine.UI;

namespace Signal.UI
{
    /// <summary>
    /// Shared health bar behavior: builds its background/fill visuals in code (so prefabs stay a
    /// bare RectTransform + component), subscribes to <see cref="HealthComponent"/> events, and
    /// animates the fill toward the latest value. Subclasses add source discovery and extras.
    /// </summary>
    public abstract class HealthBarBase : MonoBehaviour
    {
        [Header("Style")]
        [SerializeField] private Color backgroundColor = new Color(0f, 0f, 0f, 0.65f);
        [SerializeField] private Color fullColor = new Color(0.25f, 0.8f, 0.35f);
        [SerializeField] private Color lowColor = new Color(0.85f, 0.2f, 0.15f);
        [SerializeField, Min(0.1f)]
        [Tooltip("How fast the fill animates, in bar-fractions per second.")]
        private float animationSpeed = 3f;

        private HealthComponent _health;
        private RectTransform _fill;
        private Image _fillImage;
        private float _displayed = 1f;
        private float _target = 1f;

        protected HealthComponent Health => _health;

        protected virtual void Awake()
        {
            Image background = UiBuilder.NewChild<Image>(transform, "Background");
            background.color = backgroundColor;
            UiBuilder.Stretch(background.rectTransform);

            _fillImage = UiBuilder.NewChild<Image>(transform, "Fill");
            _fill = _fillImage.rectTransform;
            UiBuilder.Stretch(_fill);
            _fill.offsetMin = new Vector2(2f, 2f);
            _fill.offsetMax = new Vector2(-2f, -2f);
            ApplyFill();
        }

        /// <summary>Attaches this bar to a health source; snaps the fill to the current value.</summary>
        public void Bind(HealthComponent health)
        {
            Unbind();
            _health = health;
            if (_health == null) return;

            _health.HealthChanged += OnHealthChanged;
            _health.Died += HandleDied;
            _target = _displayed = Fraction(_health.CurrentHealth, _health.MaxHealth);
            OnHealthValues(_health.CurrentHealth, _health.MaxHealth);
            ApplyFill();
        }

        protected virtual void OnDestroy() => Unbind();

        private void Unbind()
        {
            if (_health == null) return;
            _health.HealthChanged -= OnHealthChanged;
            _health.Died -= HandleDied;
            _health = null;
        }

        private void OnHealthChanged(float current, float max)
        {
            _target = Fraction(current, max);
            OnHealthValues(current, max);
        }

        private void HandleDied() => OnDied();

        /// <summary>Raised on bind and on every health change with the raw values.</summary>
        protected virtual void OnHealthValues(float current, float max) { }

        protected virtual void OnDied() { }

        protected virtual void Update()
        {
            if (Mathf.Approximately(_displayed, _target)) return;
            _displayed = Mathf.MoveTowards(_displayed, _target, animationSpeed * Time.deltaTime);
            ApplyFill();
        }

        private void ApplyFill()
        {
            if (_fill == null) return;
            _fill.anchorMax = new Vector2(Mathf.Clamp01(_displayed), 1f);
            _fillImage.color = Color.Lerp(lowColor, fullColor, _displayed);
        }

        private static float Fraction(float current, float max) => max > 0f ? Mathf.Clamp01(current / max) : 0f;
    }
}
