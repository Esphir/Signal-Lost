using System.Collections.Generic;
using UnityEngine;

namespace Signal.Loot
{
    /// <summary>
    /// Minimal object pool for loot pickups, keyed by prefab. Released instances are deactivated
    /// and reused by later spawns; destroyed instances (scene unloads) are skipped on pop.
    /// </summary>
    public static class LootPool
    {
        private static readonly Dictionary<LootPickup, Stack<LootPickup>> Pools =
            new Dictionary<LootPickup, Stack<LootPickup>>();

        public static LootPickup Spawn(LootPickup prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null) return null;

            if (!Pools.TryGetValue(prefab, out Stack<LootPickup> pool))
                Pools[prefab] = pool = new Stack<LootPickup>();

            LootPickup instance = null;
            while (pool.Count > 0 && instance == null)
                instance = pool.Pop(); // destroyed instances pop out as null

            if (instance == null)
            {
                instance = Object.Instantiate(prefab, position, rotation);
                instance.PoolPrefab = prefab;
            }
            else
            {
                instance.transform.SetPositionAndRotation(position, rotation);
                instance.gameObject.SetActive(true);
            }

            return instance;
        }

        public static void Release(LootPickup instance)
        {
            if (instance == null) return;
            instance.gameObject.SetActive(false);

            if (instance.PoolPrefab != null && Pools.TryGetValue(instance.PoolPrefab, out Stack<LootPickup> pool))
                pool.Push(instance);
            else
                Object.Destroy(instance.gameObject);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Pools.Clear();
    }
}
