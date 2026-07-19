using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Signal.Generation;
using UnityEditor;
using UnityEngine;

namespace Signal.GenerationEditor
{
    /// <summary>
    /// The bridge between a hand-built room prefab and the connector-graph generator. It reads the
    /// doorways an artist already placed (any child whose name contains "Door" — e.g. a "Blocked Door"
    /// panel) and wires a <see cref="RoomConnector"/> onto each, so the prefab becomes generator-ready
    /// without touching a line of runtime code. Re-runnable: it owns a single "Connectors" child and
    /// rebuilds it every pass, so nothing accumulates.
    ///
    /// The whole authoring loop is therefore: duplicate a prefab, drop your geometry and Door panels in,
    /// pick a RoomType on its RoomDefinition, run "Setup Connectors", run "Populate Database". Done.
    /// </summary>
    public static class RoomAuthoringTools
    {
        private const string RoomsFolder = "Assets/Prefabs/Rooms";
        private const string ContainerName = "Connectors";
        private const float SameSpotEpsilon = 0.5f;

        // ── Menus ───────────────────────────────────────────────────────────────

        [MenuItem("Tools/Signal Lost/Rooms/Setup Connectors (Selected Prefabs)")]
        public static void SetupSelected()
        {
            List<string> paths = SelectedPrefabPaths();
            if (paths.Count == 0)
            {
                EditorUtility.DisplayDialog("Room Connectors",
                    "Select one or more room prefabs in the Project window first.", "OK");
                return;
            }
            RunSetup(paths);
        }

        [MenuItem("Tools/Signal Lost/Rooms/Setup Connectors (All in Rooms Folder)")]
        public static void SetupAllInFolder() => RunSetup(RoomPrefabPaths());

        [MenuItem("Tools/Signal Lost/Rooms/Populate Database")]
        public static void PopulateDatabase()
        {
            RoomDatabase db = FindDatabase();
            if (db == null)
            {
                EditorUtility.DisplayDialog("Room Database",
                    "No RoomDatabase asset found in the project. Create one via " +
                    "Create ▸ Signal Lost ▸ Generation ▸ Room Database first.", "OK");
                return;
            }

            int added = AddRoomsToDatabase(db, RoomPrefabPaths(), out int skipped);
            Debug.Log($"[Rooms] Database '{db.name}': added {added} room(s), {skipped} already present.", db);
            EditorUtility.DisplayDialog("Room Database",
                $"Added {added} new room(s) to '{db.name}'.\n{skipped} were already listed.", "OK");
        }

        [MenuItem("Tools/Signal Lost/Rooms/Validate")]
        public static void Validate()
        {
            var sb = new StringBuilder();
            int problems = 0;
            foreach (string path in RoomPrefabPaths())
                problems += ValidatePrefab(path, sb);

            RoomDatabase db = FindDatabase();
            if (db == null) { sb.AppendLine("✗ No RoomDatabase asset found."); problems++; }
            else if (db.Rooms.Count == 0) { sb.AppendLine("✗ RoomDatabase is empty — run Populate Database."); problems++; }

            Debug.Log($"[Rooms] Validation — {problems} problem(s):\n{sb}");
            EditorUtility.DisplayDialog("Room Validation",
                problems == 0 ? "All rooms look good. See Console for the breakdown."
                              : $"{problems} problem(s) found. See Console for details.", "OK");
        }

        [MenuItem("Tools/Signal Lost/Rooms/Setup Everything (Connectors + Database)")]
        public static void SetupEverything()
        {
            RunSetup(RoomPrefabPaths());
            PopulateDatabase();
            Validate();
        }

        // ── Connector setup ─────────────────────────────────────────────────────

        private static void RunSetup(List<string> paths)
        {
            var sb = new StringBuilder();
            foreach (string path in paths)
            {
                sb.AppendLine($"{Path.GetFileNameWithoutExtension(path)}:");
                sb.Append(SetupPrefab(path));
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[Rooms] Setup connectors on {paths.Count} prefab(s):\n{sb}");
            EditorUtility.DisplayDialog("Room Connectors",
                $"Processed {paths.Count} prefab(s). See Console for the per-room log.", "OK");
        }

        /// <summary>
        /// Loads the prefab in isolation, (re)builds one connector per door marker, and saves. Runs on
        /// prefab CONTENTS rather than an instance, so it never disturbs a scene and is safe to re-run.
        /// </summary>
        internal static string SetupPrefab(string path)
        {
            var log = new StringBuilder();
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                // One RoomDefinition, on the root. A stray one on a child (a common duplicate-prefab
                // mistake) confuses the generator, so it's removed here.
                RoomDefinition def = root.GetComponent<RoomDefinition>();
                if (def == null) { def = root.AddComponent<RoomDefinition>(); log.AppendLine("  + added RoomDefinition to root"); }
                foreach (RoomDefinition d in root.GetComponentsInChildren<RoomDefinition>(true))
                    if (d != null && d.gameObject != root)
                    {
                        log.AppendLine($"  - removed a second RoomDefinition on '{d.gameObject.name}'");
                        Object.DestroyImmediate(d, true);
                    }

                if (!TryComputeBounds(root, out Bounds worldBounds))
                {
                    log.AppendLine("  ! no renderers found — can't work out the walls, so no connectors were placed");
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    return log.ToString();
                }

                List<Transform> doors = FindDoorMarkers(root);
                if (doors.Count == 0)
                    log.AppendLine("  ! no children named 'Door' / 'Blocked Door' found — add door markers, then re-run");

                // Rebuild our own container so re-runs never stack duplicates.
                Transform old = root.transform.Find(ContainerName);
                if (old != null) Object.DestroyImmediate(old.gameObject);
                var container = new GameObject(ContainerName);
                container.transform.SetParent(root.transform, false);

                var perDirection = new Dictionary<ConnectorDirection, int>();
                var placed = new List<(ConnectorDirection dir, Vector3 pos)>();
                foreach (Transform door in doors)
                {
                    // Work from the door mesh's actual world centre, NOT its transform pivot — a ProBuilder
                    // panel's pivot is often nowhere near the panel itself. The wall that centre is nearest
                    // decides direction and where the connector snaps; snapping onto that boundary face is
                    // what guarantees rooms tile without overlapping.
                    Vector3 doorCenter = DoorMeshCenter(door);
                    ResolveOpening(doorCenter, door.name, worldBounds, out ConnectorDirection dir,
                                   out Vector3 cardinal, out Vector3 snapped, out string note);
                    if (note != null) log.AppendLine($"  ! '{door.name}' {note}");

                    foreach ((ConnectorDirection _, Vector3 pos) in placed)
                        if (Vector3.Distance(pos, snapped) < 0.5f)
                            log.AppendLine($"  ! '{door.name}' lands on the same spot as another door — give each its own opening");
                    placed.Add((dir, snapped));

                    var go = new GameObject($"Connector_{dir}");
                    go.transform.SetParent(container.transform, false);
                    go.transform.position = snapped;                             // exactly on the wall face
                    go.transform.rotation = Quaternion.LookRotation(cardinal);   // +Z points OUT
                    WireConnector(go.AddComponent<RoomConnector>(), dir, door.gameObject);

                    perDirection.TryGetValue(dir, out int n);
                    perDirection[dir] = n + 1;
                    log.AppendLine($"  + Connector_{dir}  ← '{door.name}'");
                }

                foreach (KeyValuePair<ConnectorDirection, int> kv in perDirection)
                    if (kv.Value > 1)
                        log.AppendLine($"  ! {kv.Value} doors face {kv.Key}; that's fine only if they're on different walls at different spots");

                // Tighten the collision footprint to the doors. Geometry that overhangs past a door — a
                // wall that sticks out beyond its own opening — must not count as occupied space, or a
                // neighbour mating at that door would overlap-reject against thin air. Each door pulls its
                // own wall face in to the door; walls without a door keep the full renderer extent.
                Bounds footprint = worldBounds;
                Vector3 fMin = footprint.min, fMax = footprint.max;
                foreach ((ConnectorDirection dir, Vector3 pos) in placed)
                {
                    switch (dir)
                    {
                        case ConnectorDirection.South: fMin.z = Mathf.Max(fMin.z, pos.z); break;
                        case ConnectorDirection.North: fMax.z = Mathf.Min(fMax.z, pos.z); break;
                        case ConnectorDirection.West:  fMin.x = Mathf.Max(fMin.x, pos.x); break;
                        case ConnectorDirection.East:  fMax.x = Mathf.Min(fMax.x, pos.x); break;
                    }
                }
                footprint.SetMinMax(fMin, fMax);
                WriteLocalBounds(def, root.transform, footprint);

                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            return log.ToString();
        }

        private static void WireConnector(RoomConnector connector, ConnectorDirection dir, GameObject blockingWall)
        {
            var so = new SerializedObject(connector);
            so.FindProperty("direction").enumValueIndex = (int)dir;
            so.FindProperty("connectionType").enumValueIndex = (int)ConnectionType.Standard;
            so.FindProperty("blockingWall").objectReferenceValue = blockingWall;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ── Database ────────────────────────────────────────────────────────────

        private static int AddRoomsToDatabase(RoomDatabase db, List<string> paths, out int skipped)
        {
            skipped = 0;
            var so = new SerializedObject(db);
            SerializedProperty list = so.FindProperty("rooms");

            var present = new HashSet<Object>();
            for (int i = 0; i < list.arraySize; i++)
            {
                Object p = list.GetArrayElementAtIndex(i).FindPropertyRelative("prefab").objectReferenceValue;
                if (p != null) present.Add(p);
            }

            int added = 0;
            foreach (string path in paths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null || prefab.GetComponent<RoomDefinition>() == null) continue;
                if (!present.Add(prefab)) { skipped++; continue; }

                int idx = list.arraySize;
                list.InsertArrayElementAtIndex(idx);
                SerializedProperty e = list.GetArrayElementAtIndex(idx);
                e.FindPropertyRelative("prefab").objectReferenceValue = prefab;
                e.FindPropertyRelative("weight").floatValue = 1f;
                e.FindPropertyRelative("minRoomIndex").intValue = 0;
                e.FindPropertyRelative("maxRoomIndex").intValue = 0;
                added++;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            return added;
        }

        // ── Validation ──────────────────────────────────────────────────────────

        private static int ValidatePrefab(string path, StringBuilder sb)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) return 0;

            string name = prefab.name;
            int problems = 0;

            if (prefab.GetComponent<RoomDefinition>() == null)
            {
                sb.AppendLine($"✗ {name}: no RoomDefinition on the root.");
                problems++;
            }

            RoomDefinition[] defs = prefab.GetComponentsInChildren<RoomDefinition>(true);
            if (defs.Length > 1)
            {
                sb.AppendLine($"✗ {name}: {defs.Length} RoomDefinition components — keep exactly one, on the root.");
                problems++;
            }

            RoomConnector[] conns = prefab.GetComponentsInChildren<RoomConnector>(true);
            if (conns.Length == 0)
            {
                sb.AppendLine($"✗ {name}: no RoomConnectors — run Setup Connectors.");
                return problems + 1;
            }

            for (int i = 0; i < conns.Length; i++)
            {
                if (new SerializedObject(conns[i]).FindProperty("blockingWall").objectReferenceValue == null)
                {
                    sb.AppendLine($"⚠ {name}: connector '{conns[i].name}' has no blocking wall — an unused door here would open into the void.");
                    problems++;
                }
                for (int j = i + 1; j < conns.Length; j++)
                    if (Vector3.Distance(conns[i].transform.position, conns[j].transform.position) < SameSpotEpsilon)
                    {
                        sb.AppendLine($"⚠ {name}: '{conns[i].name}' and '{conns[j].name}' share a position — move each door to its own opening.");
                        problems++;
                    }
            }

            if (problems == 0)
            {
                RoomDefinition def = prefab.GetComponent<RoomDefinition>();
                sb.AppendLine($"✓ {name}: {def.RoomType}, {conns.Length} connector(s).");
            }
            return problems;
        }

        // ── Helpers ─────────────────────────────────────────────────────────────

        /// <summary>Topmost children whose name contains "door", so a door mesh with sub-parts counts once.</summary>
        internal static List<Transform> FindDoorMarkers(GameObject root)
        {
            var matched = new List<Transform>();
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t == root.transform) continue;
                string n = t.name.ToLowerInvariant();
                if (!n.Contains("door") || n.StartsWith("connector")) continue;
                matched.Add(t);
            }

            var topmost = new List<Transform>();
            foreach (Transform t in matched)
            {
                bool nested = false;
                for (Transform p = t.parent; p != null; p = p.parent)
                    if (matched.Contains(p)) { nested = true; break; }
                if (!nested) topmost.Add(t);
            }
            return topmost;
        }

        /// <summary>
        /// Reads a cardinal from the door's name — the authoritative source when present. Accepts a
        /// trailing token like Door_N / Door_South, and ignores any trailing " (1)" Unity adds to copies.
        /// </summary>
        private static bool TryDirectionFromName(string name, out ConnectorDirection dir, out Vector3 cardinal)
        {
            dir = default;
            cardinal = default;

            // Drop a trailing "(1)", digits and spaces so "Door_N (2)" still ends in the direction token.
            string n = Regex.Replace(name.ToLowerInvariant(), @"[\s\(\)\d]+$", "");

            if (n.EndsWith("north") || n.EndsWith("_n")) { dir = ConnectorDirection.North; cardinal = Vector3.forward; return true; }
            if (n.EndsWith("south") || n.EndsWith("_s")) { dir = ConnectorDirection.South; cardinal = Vector3.back; return true; }
            if (n.EndsWith("east")  || n.EndsWith("_e")) { dir = ConnectorDirection.East;  cardinal = Vector3.right; return true; }
            if (n.EndsWith("west")  || n.EndsWith("_w")) { dir = ConnectorDirection.West;  cardinal = Vector3.left; return true; }
            return false;
        }

        /// <summary>
        /// Resolves a door into a connector by the wall it's on. The connector snaps onto the nearest of
        /// the room's four side faces, which is what makes rooms tile without overlap — the mating point
        /// is always on the boundary, never inside. The name (Door_N…) only breaks ties at corners and
        /// flags mismatches; geometry wins, because the real hole is wherever the door actually sits.
        /// </summary>
        private static void ResolveOpening(Vector3 p, string doorName, Bounds bounds, out ConnectorDirection dir,
                                           out Vector3 cardinal, out Vector3 snapped, out string note)
        {
            note = null;

            var faces = new (ConnectorDirection d, float dist, Vector3 card, float coord, bool axisX)[]
            {
                (ConnectorDirection.West,  Mathf.Abs(p.x - bounds.min.x), Vector3.left,    bounds.min.x, true),
                (ConnectorDirection.East,  Mathf.Abs(p.x - bounds.max.x), Vector3.right,   bounds.max.x, true),
                (ConnectorDirection.South, Mathf.Abs(p.z - bounds.min.z), Vector3.back,    bounds.min.z, false),
                (ConnectorDirection.North, Mathf.Abs(p.z - bounds.max.z), Vector3.forward, bounds.max.z, false),
            };

            int best = 0;
            for (int i = 1; i < faces.Length; i++)
                if (faces[i].dist < faces[best].dist) best = i;

            // A named direction wins when it's about as near as the geometric best (a corner door); if it
            // clearly disagrees, keep the geometry and warn — the connector belongs where the hole is.
            if (TryDirectionFromName(doorName, out ConnectorDirection named, out _))
            {
                int idx = System.Array.FindIndex(faces, f => f.d == named);
                if (idx >= 0 && faces[idx].dist <= faces[best].dist + 2f) best = idx;
                else if (named != faces[best].d) note = $"is named {named} but sits on the {faces[best].d} wall — using {faces[best].d}. Move it to the {named} wall if that's wrong.";
            }

            var chosen = faces[best];
            dir = chosen.d;
            cardinal = chosen.card;

            // Snap onto the wall to clean up sub-unit float error, but only when the door is essentially
            // on it. A door set well back from the bounds (the room's geometry overhangs past its own
            // door) must keep its real position — otherwise the opening drifts off the door and leaves a
            // gap where the next room meets it.
            const float onWall = 2f;
            float perp = chosen.axisX ? p.x : p.z;
            float coord = chosen.dist <= onWall ? chosen.coord : perp;
            if (chosen.dist > onWall)
                note = (note == null ? "" : note + "; ") +
                       $"sits {chosen.dist:0.#} in from the {chosen.d} wall — the room overhangs its own door, so it may not meet the next room flush there";

            snapped = chosen.axisX ? new Vector3(coord, p.y, p.z) : new Vector3(p.x, p.y, coord);
        }

        internal static bool TryComputeBounds(GameObject root, out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                if (!any) { bounds = r.bounds; any = true; }
                else bounds.Encapsulate(r.bounds);
            }
            return any;
        }

        /// <summary>
        /// Where a door's opening actually is, in world space: horizontally the centre of the door mesh,
        /// vertically its base (the threshold). ProBuilder pivots often sit nowhere near the mesh, so the
        /// renderer bounds are the real location; anchoring at the base means two rooms line up floor-to-
        /// floor when they mate, whatever their door heights. Falls back to the pivot if nothing renders.
        /// </summary>
        private static Vector3 DoorMeshCenter(Transform door)
        {
            Renderer[] renderers = door.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return door.position;

            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return new Vector3(b.center.x, b.min.y, b.center.z);
        }

        private static void WriteLocalBounds(RoomDefinition def, Transform root, Bounds world)
        {
            Vector3 localCenter = root.InverseTransformPoint(world.center);
            Vector3 s = root.lossyScale;
            Vector3 localSize = new Vector3(
                Mathf.Abs(s.x) > 1e-4f ? world.size.x / Mathf.Abs(s.x) : world.size.x,
                Mathf.Abs(s.y) > 1e-4f ? world.size.y / Mathf.Abs(s.y) : world.size.y,
                Mathf.Abs(s.z) > 1e-4f ? world.size.z / Mathf.Abs(s.z) : world.size.z);

            var so = new SerializedObject(def);
            SerializedProperty b = so.FindProperty("localBounds");
            b.FindPropertyRelative("m_Center").vector3Value = localCenter;
            b.FindPropertyRelative("m_Extent").vector3Value = localSize * 0.5f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static List<string> SelectedPrefabPaths()
        {
            var paths = new List<string>();
            foreach (Object obj in Selection.objects)
            {
                string p = AssetDatabase.GetAssetPath(obj);
                if (!string.IsNullOrEmpty(p) && p.EndsWith(".prefab")) paths.Add(p);
            }
            return paths;
        }

        private static List<string> RoomPrefabPaths()
        {
            var paths = new List<string>();
            if (!AssetDatabase.IsValidFolder(RoomsFolder)) return paths;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { RoomsFolder }))
                paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            return paths;
        }

        internal static RoomDatabase FindDatabase()
        {
            string[] guids = AssetDatabase.FindAssets("t:RoomDatabase");
            if (guids.Length == 0) return null;
            if (guids.Length > 1)
                Debug.LogWarning($"[Rooms] {guids.Length} RoomDatabase assets exist; using the first. Delete the extras or populate the right one by hand.");
            return AssetDatabase.LoadAssetAtPath<RoomDatabase>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }
    }
}
