// The boss's health across the bottom of the screen: a named bar that drains smoothly and disappears the moment the fight is over.
using Signal.Combat.Interfaces;
using UnityEngine;
using UnityEngine.UI;

namespace Signal.UI
{
    public sealed class BossHealthBarUI : MonoBehaviour
    {
        private static readonly Color FrameColor = new Color(0.05f, 0.03f, 0.03f, 0.85f);
        private static readonly Color FillColor = new Color(0.78f, 0.13f, 0.09f);
        private static readonly Color EnragedColor = new Color(1f, 0.45f, 0.08f);

        private const float DrainSpeed = 0.7f;
        private const float EmptyLinger = 0.4f;

        private static BossHealthBarUI _instance;

        private IHealth _health;
        private Object _source;
        private RectTransform _fill;
        private Image _fillImage;
        private Text _label;
        private float _target = 1f;
        private float _shown = 1f;
        private bool _dismissing;
        private float _emptyAt = -1f;

        public static void Show(IHealth health, string title)
        {
            if (health == null) return;
            if (_instance == null)
            {
                _instance = new GameObject("BossHealthBar").AddComponent<BossHealthBarUI>();
                _instance.Build();
            }
            _instance.Bind(health, title);
        }

        public static void Hide()
        {
            if (_instance != null) Destroy(_instance.gameObject);
            _instance = null;
        }

        public static void Dismiss()
        {
            if (_instance == null) return;
            _instance.BeginDismiss();
        }

        private void BeginDismiss()
        {
            if (_health != null) _health.HealthChanged -= OnHealthChanged;
            _health = null;
            _source = null;
            _dismissing = true;
            _target = 0f;
        }

        private void Bind(IHealth health, string title)
        {
            if (_health != null) _health.HealthChanged -= OnHealthChanged;

            _health = health;
            _source = health as Object;
            _health.HealthChanged += OnHealthChanged;
            _label.text = string.IsNullOrWhiteSpace(title) ? "BOSS" : title.ToUpperInvariant();

            _target = Fraction(health.CurrentHealth, health.MaxHealth);
            _shown = _target;
            Apply();
        }

        private void OnDestroy()
        {
            if (_health != null) _health.HealthChanged -= OnHealthChanged;
            if (_instance == this) _instance = null;
        }

        private void OnHealthChanged(float current, float max) => _target = Fraction(current, max);

        private void Update()
        {
            if (!_dismissing && (_health == null || _source == null)) { Hide(); return; }

            if (!Mathf.Approximately(_shown, _target))
            {
                _shown = Mathf.MoveTowards(_shown, _target, DrainSpeed * Time.unscaledDeltaTime);
                Apply();
            }
            else if (_dismissing)
            {
                if (_emptyAt < 0f) _emptyAt = Time.unscaledTime + EmptyLinger;
                else if (Time.unscaledTime >= _emptyAt) Hide();
            }
        }

        private void Apply()
        {
            _fill.anchorMax = new Vector2(Mathf.Clamp01(_shown), 1f);
            _fillImage.color = _shown <= 0.5f ? EnragedColor : FillColor;
        }

        private static float Fraction(float current, float max) => max <= 0f ? 0f : Mathf.Clamp01(current / max);

        private void Build()
        {
            UiBuilder.EnsureEventSystem();
            Canvas canvas = UiBuilder.CreateOverlayCanvas("BossHealthCanvas", 42);
            canvas.transform.SetParent(transform, false);

            RectTransform root = UiBuilder.NewRect(canvas.transform, "BossBar");
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0f);
            root.anchoredPosition = new Vector2(0f, 54f);
            root.sizeDelta = new Vector2(1100f, 72f);

            _label = UiBuilder.CreateText(root, "Name", "BOSS", 26, FontStyle.Bold, TextAnchor.LowerCenter);
            RectTransform labelRect = _label.rectTransform;
            labelRect.anchorMin = new Vector2(0f, 1f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.pivot = new Vector2(0.5f, 1f);
            labelRect.sizeDelta = new Vector2(0f, 36f);
            labelRect.anchoredPosition = Vector2.zero;
            _label.color = new Color(0.95f, 0.85f, 0.8f);

            Image frame = UiBuilder.NewChild<Image>(root, "Frame");
            frame.color = FrameColor;
            RectTransform frameRect = frame.rectTransform;
            frameRect.anchorMin = new Vector2(0f, 0f);
            frameRect.anchorMax = new Vector2(1f, 0f);
            frameRect.pivot = new Vector2(0.5f, 0f);
            frameRect.sizeDelta = new Vector2(0f, 28f);
            frameRect.anchoredPosition = Vector2.zero;

            RectTransform fillArea = UiBuilder.NewRect(frame.transform, "FillArea");
            UiBuilder.Stretch(fillArea);
            fillArea.offsetMin = new Vector2(4f, 4f);
            fillArea.offsetMax = new Vector2(-4f, -4f);

            _fillImage = UiBuilder.NewChild<Image>(fillArea, "Fill");
            _fillImage.color = FillColor;
            _fill = _fillImage.rectTransform;
            _fill.anchorMin = Vector2.zero;
            _fill.anchorMax = Vector2.one;
            _fill.offsetMin = Vector2.zero;
            _fill.offsetMax = Vector2.zero;
        }
    }
}
