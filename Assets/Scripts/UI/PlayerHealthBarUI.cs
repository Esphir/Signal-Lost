using Signal.Combat.Health;
using UnityEngine;
using UnityEngine.UI;

namespace Signal.UI
{
    /// <summary>
    /// Screen-space player health bar with a "current / max" label. Binds to the assigned
    /// <see cref="HealthComponent"/>, or finds the object tagged "Player" when left empty —
    /// generic enough to point at a boss or ally later.
    /// </summary>
    public class PlayerHealthBarUI : HealthBarBase
    {
        [SerializeField]
        [Tooltip("Health source to display. Left empty, the object tagged 'Player' is used.")]
        private HealthComponent healthSource;

        private Text _label;

        protected override void Awake()
        {
            base.Awake();
            _label = UiBuilder.CreateText(transform, "Label", "", 16, FontStyle.Bold, TextAnchor.MiddleCenter);
            UiBuilder.Stretch(_label.rectTransform);
        }

        private void Start()
        {
            if (healthSource == null)
            {
                GameObject player = GameObject.FindWithTag("Player");
                if (player != null) healthSource = player.GetComponent<HealthComponent>();
            }

            if (healthSource == null)
            {
                Debug.LogWarning("[UI] PlayerHealthBarUI found no HealthComponent to display.", this);
                return;
            }
            Bind(healthSource);
        }

        protected override void OnHealthValues(float current, float max)
            => _label.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
    }
}
