// Minimal object pool for one-shot VFX prefabs, keyed by prefab.
using System.Collections.Generic;
using UnityEngine;

namespace Signal.World
{
    public static class VfxPool
    {
        private static readonly Dictionary<GameObject, Stack<PooledVfx>> Pools =
            new Dictionary<GameObject, Stack<PooledVfx>>();

        public static PooledVfx Play(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null) return null;

            if (!Pools.TryGetValue(prefab, out Stack<PooledVfx> pool))
                Pools[prefab] = pool = new Stack<PooledVfx>();

            PooledVfx vfx = null;
            while (pool.Count > 0 && vfx == null) vfx = pool.Pop();

            if (vfx == null)
            {
                GameObject go = Object.Instantiate(prefab, position, rotation);
                vfx = go.GetComponent<PooledVfx>();
                if (vfx == null) vfx = go.AddComponent<PooledVfx>();
                vfx.PoolPrefab = prefab;
            }
            else
            {
                vfx.transform.SetPositionAndRotation(position, rotation);
                vfx.gameObject.SetActive(true);
            }

            vfx.Play();
            return vfx;
        }

        public static void Release(PooledVfx vfx)
        {
            if (vfx == null) return;

            vfx.transform.SetParent(null, worldPositionStays: true);
            vfx.gameObject.SetActive(false);

            if (vfx.PoolPrefab != null && Pools.TryGetValue(vfx.PoolPrefab, out Stack<PooledVfx> pool))
                pool.Push(vfx);
            else
                Object.Destroy(vfx.gameObject);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Pools.Clear();
    }
}
