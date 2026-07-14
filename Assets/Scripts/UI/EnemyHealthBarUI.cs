using Signal.Combat.Health;
using UnityEngine;

namespace Signal.UI
{
    /// <summary>
    /// World-space health bar that floats above an enemy, billboards toward the camera, and hides
    /// at full health (configurable) and on death. Destroys itself when its target is destroyed.
    /// Spawned and bound by <see cref="EnemyHealthBarSpawner"/> — reusable for bosses via a bigger
    /// prefab variant.
    /// </summary>
    public class EnemyHealthBarUI : HealthBarBase
    {
        [Header("Visibility")]
        [SerializeField]
        [Tooltip("Keep the bar hidden while the enemy is at full health.")]
        private bool hideWhenFull = true;

        [Header("Billboard")]
        [SerializeField]
        [Tooltip("Camera to face. Left empty, Camera.main is used.")]
        private Camera billboardCamera;

        private Canvas _canvas;
        private Transform _followTarget;
        private float _heightOffset;

        protected override void Awake()
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            base.Awake();
            _canvas.enabled = false; // hidden until bound
        }

        /// <summary>Binds the bar to an enemy: health source, transform to follow, and world-space height.</summary>
        public void Bind(HealthComponent health, Transform followTarget, float heightOffset, Camera cameraOverride)
        {
            _followTarget = followTarget;
            _heightOffset = heightOffset;
            if (cameraOverride != null) billboardCamera = cameraOverride;
            Bind(health);
        }

        protected override void OnHealthValues(float current, float max)
            => _canvas.enabled = current > 0f && !(hideWhenFull && current >= max - 0.01f);

        protected override void OnDied() => _canvas.enabled = false;

        private void LateUpdate()
        {
            if (_followTarget == null)
            {
                Destroy(gameObject); // enemy was destroyed
                return;
            }

            transform.position = _followTarget.position + Vector3.up * _heightOffset;

            if (billboardCamera == null) billboardCamera = Camera.main;
            if (billboardCamera != null)
                transform.rotation = Quaternion.LookRotation(transform.position - billboardCamera.transform.position);
        }
    }
}
