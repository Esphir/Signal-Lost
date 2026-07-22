// Turns the single 4 Door Room into a small family of rooms so the generator has variety to draw from: a Combat junction (three ways out, enemies inside), a Treasure dead-end (one way out), and an End room (one way out, rolls a fresh layout on entry).
using System.Collections.Generic;
using System.Text;
using Signal.Generation;
using Signal.Spawning;
using UnityEditor;
using UnityEngine;

namespace Signal.GenerationEditor
{
    public static class RoomVariantTools
    {
        private const string BaseRoom = "Assets/Prefabs/Rooms/4 Door Room.prefab";
        private const string RoomsFolder = "Assets/Prefabs/Rooms";
        private const string ProfilePath = "Assets/Scripts/Spawning/DefaultEnemySpawnProfile.asset";

        [MenuItem("Tools/Signal Lost/Rooms/Create 4-Door Variants (Combat, Treasure, End)")]
        public static void CreateVariants()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(BaseRoom) == null)
            {
                EditorUtility.DisplayDialog("Room Variants", $"Base room not found at {BaseRoom}.", "OK");
                return;
            }

            var log = new StringBuilder();
            var made = new List<string>();

            TryMake(made, log, "Combat Room",   RoomType.Combat,    3,    true,    false);
            TryMake(made, log, "Treasure Room", RoomType.Treasure,  1,    false,   false);
            TryMake(made, log, "End Room",      RoomType.End,       1,    false,   true);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            foreach (string path in made) log.Append(RoomAuthoringTools.SetupPrefab(path));
            AssetDatabase.SaveAssets();

            Debug.Log($"[Rooms] Variant creation:\n{log}");

            if (made.Count > 0) RoomAuthoringTools.PopulateDatabase();
            EditorUtility.DisplayDialog("Room Variants",
                made.Count > 0
                    ? $"Created {made.Count} room(s), wired connectors, and added them to the database. See Console."
                    : "Nothing to do — all variants already exist. Delete a variant prefab to recreate it.",
                "OK");
        }

        private static void TryMake(List<string> made, StringBuilder log, string name, RoomType type,
                                    int keepOpen, bool enemies, bool end)
        {
            string path = $"{RoomsFolder}/{name}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            {
                log.AppendLine($"{name}: already exists — left as-is.");
                return;
            }
            if (!AssetDatabase.CopyAsset(BaseRoom, path))
            {
                log.AppendLine($"{name}: FAILED to copy base room.");
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            log.AppendLine($"{name} ({type}):");
            try
            {
                root.name = name;

                RoomDefinition def = root.GetComponent<RoomDefinition>();
                if (def != null)
                {
                    var so = new SerializedObject(def);
                    so.FindProperty("roomType").enumValueIndex = (int)type;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                List<Transform> doors = RoomAuthoringTools.FindDoorMarkers(root);
                int kept = Mathf.Min(keepOpen, doors.Count);
                for (int i = kept; i < doors.Count; i++)
                {
                    doors[i].name = "Sealed Wall";
                }
                log.AppendLine($"  {kept} door(s) open, {Mathf.Max(0, doors.Count - kept)} sealed");

                if (enemies) AddSpawnSection(root, log);
                if (end) AddEndTrigger(root, log);

                PrefabUtility.SaveAsPrefabAsset(root, path);
                made.Add(path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void AddSpawnSection(GameObject root, StringBuilder log)
        {
            if (!RoomAuthoringTools.TryComputeBounds(root, out Bounds b))
            {
                log.AppendLine("  ! no renderers — skipped spawn section");
                return;
            }

            var profile = AssetDatabase.LoadAssetAtPath<EnemySpawnProfile>(ProfilePath);
            if (profile == null) log.AppendLine($"  ! spawn profile not found at {ProfilePath} — section added without one");

            var section = new GameObject("Spawn Section");
            section.transform.SetParent(root.transform, true);
            section.transform.position = new Vector3(b.center.x, b.min.y, b.center.z);

            var box = section.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0f, 3f, 0f);
            box.size = new Vector3(b.size.x * 0.85f, 6f, b.size.z * 0.85f);

            var sec = section.AddComponent<EnemySpawnSection>();
            var so = new SerializedObject(sec);
            if (profile != null) so.FindProperty("spawnProfile").objectReferenceValue = profile;
            so.FindProperty("triggerCollider").objectReferenceValue = box;
            so.FindProperty("minEnemyCount").intValue = 3;
            so.FindProperty("maxEnemyCount").intValue = 6;
            so.ApplyModifiedPropertiesWithoutUndo();

            float x = b.size.x * 0.2f;
            float z = b.size.z * 0.2f;
            Vector2[] spread = { new Vector2(-x, -z), new Vector2(x, -z), new Vector2(-x, z), new Vector2(x, z) };
            foreach (Vector2 o in spread)
            {
                var pt = new GameObject("Spawn Point");
                pt.transform.SetParent(section.transform, true);
                pt.transform.position = new Vector3(b.center.x + o.x, b.min.y + 0.5f, b.center.z + o.y);
                pt.AddComponent<EnemySpawnPoint>();
            }

            log.AppendLine("  + Spawn Section (trigger + 4 points, 3–6 enemies)");
        }

        private static void AddEndTrigger(GameObject root, StringBuilder log)
        {
            if (!RoomAuthoringTools.TryComputeBounds(root, out Bounds b))
            {
                log.AppendLine("  ! no renderers — skipped end trigger");
                return;
            }

            var trigger = new GameObject("End Trigger");
            trigger.transform.SetParent(root.transform, true);
            trigger.transform.position = new Vector3(b.center.x, b.min.y, b.center.z);

            var box = trigger.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0f, 3f, 0f);
            box.size = new Vector3(b.size.x * 0.7f, 6f, b.size.z * 0.7f);

            trigger.AddComponent<EndRoomTrigger>();
            log.AppendLine("  + End Trigger (rolls a fresh layout on entry)");
        }
    }
}
