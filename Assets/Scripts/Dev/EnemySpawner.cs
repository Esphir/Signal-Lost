using System;
using System.Collections.Generic;
using Signal.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Signal.Dev
{
    /// <summary>
    /// Developer "Enemies" tab: spawns enemies from a configurable Inspector list (no hardcoded
    /// types — drop any enemy prefab in and it appears). Places the enemy in front of the player,
    /// snapped to the ground, nudged clear of other colliders.
    /// </summary>
    public class EnemySpawner : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Enemy prefabs offered in the developer menu. Add future enemies here — no code changes needed.")]
        private GameObject[] enemyPrefabs;

        [SerializeField, Min(0.5f)] private float spawnDistance = 5f;
        [SerializeField, Min(0f)] private float verticalOffset = 1f;
        [SerializeField]
        [Tooltip("Layers used to snap the spawn point to the ground.")]
        private LayerMask groundMask = ~0;
        [SerializeField]
        [Tooltip("Layers that block a spawn point (the spawn is nudged forward to avoid them).")]
        private LayerMask obstacleMask = ~0;

        private static readonly Color ButtonColor = new Color(0.16f, 0.16f, 0.22f);

        public void BuildEnemiesTab(Transform parent)
        {
            if (enemyPrefabs == null || enemyPrefabs.Length == 0)
            {
                UiBuilder.CreateText(parent, "Empty", "No enemy prefabs assigned in the Inspector.", 18, FontStyle.Italic, TextAnchor.MiddleLeft)
                    .gameObject.AddComponent<LayoutElement>().preferredHeight = 40f;
                return;
            }

            foreach (GameObject prefab in enemyPrefabs)
            {
                if (prefab == null) continue;
                GameObject captured = prefab;
                Button button = UiBuilder.CreateButton(parent, $"Spawn_{prefab.name}", $"Spawn {prefab.name}", ButtonColor, 18, out _);
                button.gameObject.AddComponent<LayoutElement>().preferredHeight = 40f;
                button.onClick.AddListener(() => Spawn(captured));
            }
        }

        public void Spawn(GameObject prefab)
        {
            if (prefab == null) return;
            GameObject player = GameObject.FindWithTag("Player");
            if (player == null)
            {
                Debug.LogWarning("[Dev] EnemySpawner: no Player found to spawn in front of.", this);
                return;
            }

            Vector3 forward = player.transform.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.forward;

            Vector3 point = player.transform.position + forward * spawnDistance;
            if (Physics.Raycast(point + Vector3.up * 6f, Vector3.down, out RaycastHit hit, 30f, groundMask, QueryTriggerInteraction.Ignore))
                point = hit.point;
            point += Vector3.up * verticalOffset;

            for (int i = 0; i < 6 && Physics.CheckSphere(point, 0.5f, obstacleMask, QueryTriggerInteraction.Ignore); i++)
                point += forward * 1.2f;

            Instantiate(prefab, point, Quaternion.LookRotation(-forward));
            Debug.Log($"[Dev] Spawned '{prefab.name}' at {point}.");
        }
    }
}
