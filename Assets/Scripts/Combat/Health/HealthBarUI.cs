// Minimal example of UI binding to IHealth — subscribes to events rather than polling every frame.
using UnityEngine;
using UnityEngine.UI;
using Signal.Combat.Interfaces;

namespace Signal.Combat.Health
{
    public class HealthBarUI : MonoBehaviour
    {
        [SerializeField] private Slider slider;
        [Tooltip("Any component implementing IHealth (e.g. the player's HealthComponent).")]
        [SerializeField] private MonoBehaviour healthSource;

        private IHealth _health;

        private void OnEnable()
        {
            _health = healthSource as IHealth;
            if (_health == null) return;

            _health.HealthChanged += HandleHealthChanged;
            HandleHealthChanged(_health.CurrentHealth, _health.MaxHealth);
        }

        private void OnDisable()
        {
            if (_health != null)
                _health.HealthChanged -= HandleHealthChanged;
        }

        private void HandleHealthChanged(float current, float max)
        {
            if (slider == null) return;
            slider.maxValue = max;
            slider.value = current;
        }
    }
}
