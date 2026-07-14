using Signal.Combat.Health;
using UnityEngine;

namespace Signal.UI
{
    /// <summary>
    /// Attach next to an enemy's HealthComponent: spawns its world-space health bar on startup so
    /// every enemy instance (scene-placed or runtime-spawned) gets one automatically.
    /// </summary>
    public class EnemyHealthBarSpawner : MonoBehaviour
    {
        [SerializeField] private EnemyHealthBarUI barPrefab;
        [SerializeField, Min(0f)]
        [Tooltip("World-space height of the bar above this enemy's origin.")]
        private float heightOffset = 2.2f;
        [SerializeField]
        [Tooltip("Optional camera for the billboard; Camera.main when empty.")]
        private Camera billboardCamera;

        private void Start()
        {
            var health = GetComponent<HealthComponent>();
            if (barPrefab == null || health == null)
            {
                Debug.LogWarning($"[UI] EnemyHealthBarSpawner on '{name}' needs a bar prefab and a HealthComponent.", this);
                return;
            }

            EnemyHealthBarUI bar = Instantiate(barPrefab);
            bar.name = $"{name} HealthBar";
            bar.Bind(health, transform, heightOffset, billboardCamera);
        }
    }
}
