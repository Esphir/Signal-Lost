// A respawn checkpoint.
using UnityEngine;
using UnityEngine.Events;

namespace Signal.World
{
    [DisallowMultipleComponent]
    public class Checkpoint : MonoBehaviour
    {
        [Header("Spawn")]
        [SerializeField]
        [Tooltip("Where the player respawns for this checkpoint. Empty = this transform.")]
        private Transform spawnTransform;
        [SerializeField, Min(0.1f)]
        [Tooltip("Radius of the auto-created activation trigger (ignored if the object already has a trigger collider).")]
        private float activationRadius = 1.5f;

        [Header("Respawn VFX")]
        [SerializeField]
        [Tooltip("Optional VFX used instead of the RespawnManager's default when respawning here.")]
        private GameObject respawnVfxOverride;

        [Header("Events")]
        public UnityEvent Activated;
        public UnityEvent Deactivated;

        public Vector3 SpawnPosition => spawnTransform != null ? spawnTransform.position : transform.position;
        public Quaternion SpawnRotation => spawnTransform != null ? spawnTransform.rotation : transform.rotation;
        public GameObject RespawnVfxOverride => respawnVfxOverride;
        public bool IsActive { get; private set; }

        private void Reset() => spawnTransform = transform;

        private void Awake()
        {
            if (spawnTransform == null) spawnTransform = transform;
            EnsureTrigger();
        }

        private void EnsureTrigger()
        {
            foreach (Collider c in GetComponents<Collider>())
                if (c.isTrigger) return;

            var sphere = gameObject.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            float scale = Mathf.Max(0.01f, transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z);
            sphere.radius = activationRadius / scale;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            if (RespawnManager.Instance != null) RespawnManager.Instance.SetActiveCheckpoint(this);
        }

        internal void Activate()
        {
            if (IsActive) return;
            IsActive = true;
            Activated?.Invoke();
        }

        internal void Deactivate()
        {
            if (!IsActive) return;
            IsActive = false;
            Deactivated?.Invoke();
        }

        private void OnDestroy()
        {
            if (RespawnManager.Instance != null) RespawnManager.Instance.ClearCheckpointIfCurrent(this);
        }
    }
}
