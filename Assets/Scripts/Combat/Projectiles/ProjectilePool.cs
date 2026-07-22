// Object pool for LobProjectile.
using UnityEngine;
using UnityEngine.Pool;

namespace Signal.Combat.Projectiles
{
    public class ProjectilePool : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Prefab to pool. If left empty, the shooter injects its own projectile prefab.")]
        private LobProjectile projectilePrefab;

        [SerializeField, Min(1)] private int defaultCapacity = 8;
        [SerializeField, Min(1)] private int maxSize = 32;

        private ObjectPool<LobProjectile> _pool;

        public bool HasPrefab => projectilePrefab != null;

        public void SetPrefab(LobProjectile prefab)
        {
            if (projectilePrefab == null) projectilePrefab = prefab;
        }

        public LobProjectile Spawn(Vector3 position, Quaternion rotation)
        {
            if (projectilePrefab == null)
            {
                Debug.LogError("[Combat] ProjectilePool has no prefab assigned.", this);
                return null;
            }

            _pool ??= new ObjectPool<LobProjectile>(
                Create, OnGet, OnRelease, OnDestroyItem, false, defaultCapacity, maxSize);

            LobProjectile projectile = _pool.Get();

            projectile.PlaceAt(position, rotation);
            return projectile;
        }

        private LobProjectile Create()
        {
            LobProjectile projectile = Instantiate(projectilePrefab);
            projectile.SetDespawnHandler(p => _pool.Release(p));
            return projectile;
        }

        private static void OnGet(LobProjectile p) => p.gameObject.SetActive(true);
        private static void OnRelease(LobProjectile p) => p.gameObject.SetActive(false);
        private static void OnDestroyItem(LobProjectile p)
        {
            if (p != null) Destroy(p.gameObject);
        }

        private void OnDestroy() => _pool?.Dispose();
    }
}
