using System.Collections.Generic;
using System.IO;
using Signal.Generation;
using Signal.Spawning;
using Signal.World;
using UnityEditor;
using UnityEngine;

/// <summary>
/// One-shot scaffold: rebuilds the placeholder rooms for the connector-graph generator.
///
/// The important change over v1 is the door contract: every doorway has a Door Panel that is ON by
/// default and referenced as the connector's Blocking Wall. A room is therefore sealed as authored,
/// and only opens where the generator actually mated something — so an unused door can never expose
/// the void. Rooms also carry doors on more sides, which is what makes branching possible at all.
/// </summary>
public static class BuildRoomsV2
{
    private const string RoomFolder = "Assets/Prefabs/Rooms";
    private const string DatabasePath = "Assets/Scripts/Generation/RoomDatabase.asset";

    private const float Size = 20f;
    private const float Height = 6f;
    private const float Thickness = 0.5f;
    private const float DoorWidth = 5f;

    private static Material _floorMat, _wallMat, _doorMat;

    private static readonly ConnectorDirection[] AllSides =
    {
        ConnectorDirection.North, ConnectorDirection.South, ConnectorDirection.East, ConnectorDirection.West,
    };

    [MenuItem("Tools/Signal Lost/Build Rooms V2")]
    public static void Build()
    {
        Directory.CreateDirectory(RoomFolder);
        _floorMat = Mat("RoomFloor", new Color(0.30f, 0.32f, 0.36f));
        _wallMat = Mat("RoomWall", new Color(0.18f, 0.19f, 0.22f));
        _doorMat = Mat("RoomDoorPanel", new Color(0.42f, 0.24f, 0.20f)); // visibly different: a seal

        var N = ConnectorDirection.North;
        var S = ConnectorDirection.South;
        var E = ConnectorDirection.East;
        var W = ConnectorDirection.West;

        // Doors on several sides is what lets the graph branch and turn. A room with only N/S can
        // only ever extend a corridor, which is exactly what made v1 generate a straight line.
        var specs = new List<(string name, RoomType type, int tier, ConnectorDirection[] doors, Vector2Int enemies, bool checkpoint)>
        {
            ("Room_Start",            RoomType.Start,       0, new[] { N, E, W },    Vector2Int.zero,      false),
            ("Room_Combat_Corridor",  RoomType.Combat,      0, new[] { N, S },       new Vector2Int(1, 2), false),
            ("Room_Combat_Arena",     RoomType.Combat,      1, new[] { N, S, E, W }, new Vector2Int(2, 3), false),
            ("Room_Combat_Hard",      RoomType.Combat,      2, new[] { N, S, E },    new Vector2Int(3, 3), false),
            ("Room_Platforming",      RoomType.Platforming, 0, new[] { N, S, E },    Vector2Int.zero,      false),
            ("Room_Treasure",         RoomType.Treasure,    0, new[] { S },          Vector2Int.zero,      false), // leaf
            ("Room_Checkpoint",       RoomType.Checkpoint,  0, new[] { N, S, W },    Vector2Int.zero,      true),
            ("Room_Transition",       RoomType.Transition,  0, new[] { N, S, E, W }, Vector2Int.zero,      false), // hub
            ("Room_End",              RoomType.End,         0, new[] { S },          Vector2Int.zero,      false), // leaf
        };

        var entries = new List<(GameObject prefab, float weight, int minIndex)>();
        foreach (var spec in specs)
        {
            GameObject prefab = Room(spec.name, spec.type, spec.tier, spec.doors, spec.enemies, spec.checkpoint);
            int minIndex = spec.type is RoomType.Start or RoomType.End ? 0 : 1;
            entries.Add((prefab, 1f, minIndex));
        }

        BuildDatabase(entries);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[RoomsV2] Rebuilt {entries.Count} rooms with sealed doorways + multi-side connectors.");
    }

    private static GameObject Room(string name, RoomType type, int tier,
        ConnectorDirection[] doors, Vector2Int enemies, bool checkpoint)
    {
        var root = new GameObject(name);
        Floor(root.transform);

        foreach (ConnectorDirection side in AllSides)
        {
            bool hasDoor = System.Array.IndexOf(doors, side) >= 0;
            Walls(root.transform, side, hasDoor);
            if (hasDoor) Connector(root.transform, side);
        }

        if (enemies != Vector2Int.zero) SpawnSection(root.transform, enemies);
        if (checkpoint) CheckpointObject(root.transform);

        var definition = root.AddComponent<RoomDefinition>();
        var so = new SerializedObject(definition);
        so.FindProperty("roomType").enumValueIndex = (int)type;
        so.FindProperty("difficultyTier").intValue = tier;
        so.FindProperty("localBounds").boundsValue =
            new Bounds(new Vector3(0f, Height * 0.5f, 0f), new Vector3(Size, Height, Size));
        so.ApplyModifiedPropertiesWithoutUndo();

        string path = $"{RoomFolder}/{name}.prefab";
        AssetDatabase.DeleteAsset(path);
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static void Floor(Transform parent)
    {
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Floor";
        floor.layer = LayerMask.NameToLayer("Ground"); // the spawn validator's ground check needs this
        floor.transform.SetParent(parent, false);
        floor.transform.localPosition = new Vector3(0f, -Thickness * 0.5f, 0f);
        floor.transform.localScale = new Vector3(Size, Thickness, Size);
        floor.GetComponent<MeshRenderer>().sharedMaterial = _floorMat;
        floor.isStatic = true;
    }

    /// <summary>Solid wall, or two stubs leaving a gap that the door panel fills.</summary>
    private static void Walls(Transform parent, ConnectorDirection side, bool hasDoor)
    {
        Vector3 out_ = Outward(side);
        Vector3 along = Vector3.Cross(Vector3.up, out_);

        if (!hasDoor)
        {
            Piece(parent, $"Wall_{side}", out_ * (Size * 0.5f), along * Size, _wallMat, LayerMask.NameToLayer("Wall"));
            return;
        }

        float stub = (Size - DoorWidth) * 0.5f;
        float offset = (DoorWidth + stub) * 0.5f;
        Piece(parent, $"Wall_{side}_A", out_ * (Size * 0.5f) + along * offset, along * stub, _wallMat, LayerMask.NameToLayer("Wall"));
        Piece(parent, $"Wall_{side}_B", out_ * (Size * 0.5f) - along * offset, along * stub, _wallMat, LayerMask.NameToLayer("Wall"));
    }

    private static GameObject Piece(Transform parent, string name, Vector3 centre, Vector3 span, Material material, int layer)
    {
        var piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
        piece.name = name;
        piece.layer = layer;
        piece.transform.SetParent(parent, false);
        piece.transform.localPosition = new Vector3(centre.x, Height * 0.5f, centre.z);
        piece.transform.localScale = new Vector3(
            Mathf.Max(Thickness, Mathf.Abs(span.x)), Height, Mathf.Max(Thickness, Mathf.Abs(span.z)));
        piece.GetComponent<MeshRenderer>().sharedMaterial = material;
        piece.isStatic = true;
        return piece;
    }

    /// <summary>Connector + the panel that seals its doorway until something mates with it.</summary>
    private static void Connector(Transform parent, ConnectorDirection side)
    {
        Vector3 out_ = Outward(side);
        Vector3 along = Vector3.Cross(Vector3.up, out_);

        // The seal. Active by default: a room is closed as authored.
        GameObject panel = Piece(parent, $"DoorPanel_{side}", out_ * (Size * 0.5f), along * DoorWidth,
            _doorMat, LayerMask.NameToLayer("Wall"));

        var go = new GameObject($"Connector_{side}");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = out_ * (Size * 0.5f);
        go.transform.localRotation = Quaternion.LookRotation(out_, Vector3.up); // +Z points OUT

        var connector = go.AddComponent<RoomConnector>();
        var so = new SerializedObject(connector);
        so.FindProperty("direction").enumValueIndex = (int)side;
        so.FindProperty("connectionType").enumValueIndex = (int)ConnectionType.Standard;
        so.FindProperty("blockingWall").objectReferenceValue = panel;
        so.FindProperty("doorTransform").objectReferenceValue = go.transform;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SpawnSection(Transform parent, Vector2Int enemies)
    {
        var go = new GameObject("Spawn Section A");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, 1.5f, 0f);

        var box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(Size - 4f, 3f, Size - 4f);

        var section = go.AddComponent<EnemySpawnSection>();
        var points = new List<EnemySpawnPoint>();
        for (int i = 0; i < 4; i++)
        {
            var point = new GameObject($"Point {i + 1}");
            point.transform.SetParent(go.transform, false);
            float angle = i / 4f * Mathf.PI * 2f;
            point.transform.localPosition = new Vector3(Mathf.Cos(angle) * 5f, -1.5f, Mathf.Sin(angle) * 5f);
            points.Add(point.AddComponent<EnemySpawnPoint>());
        }

        var so = new SerializedObject(section);
        so.FindProperty("minEnemyCount").intValue = enemies.x;
        so.FindProperty("maxEnemyCount").intValue = enemies.y;
        so.FindProperty("triggerCollider").objectReferenceValue = box;
        so.FindProperty("spawnProfile").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<EnemySpawnProfile>("Assets/Scripts/Spawning/DefaultEnemySpawnProfile.asset");

        SerializedProperty list = so.FindProperty("spawnPoints");
        list.arraySize = points.Count;
        for (int i = 0; i < points.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = points[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void CheckpointObject(Transform parent)
    {
        var go = new GameObject("Checkpoint");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, 1f, 0f);
        go.AddComponent<Checkpoint>();
    }

    private static Vector3 Outward(ConnectorDirection side) => side switch
    {
        ConnectorDirection.North => Vector3.forward,
        ConnectorDirection.South => Vector3.back,
        ConnectorDirection.East => Vector3.right,
        _ => Vector3.left,
    };

    private static void BuildDatabase(List<(GameObject prefab, float weight, int minIndex)> entries)
    {
        var db = ScriptableObject.CreateInstance<RoomDatabase>();
        AssetDatabase.DeleteAsset(DatabasePath);
        AssetDatabase.CreateAsset(db, DatabasePath);

        var so = new SerializedObject(db);
        SerializedProperty rooms = so.FindProperty("rooms");
        rooms.arraySize = entries.Count;

        for (int i = 0; i < entries.Count; i++)
        {
            SerializedProperty element = rooms.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("prefab").objectReferenceValue = entries[i].prefab;
            element.FindPropertyRelative("weight").floatValue = entries[i].weight;
            element.FindPropertyRelative("minRoomIndex").intValue = entries[i].minIndex;
            element.FindPropertyRelative("maxRoomIndex").intValue = 0;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Material Mat(string name, Color color)
    {
        string path = $"{RoomFolder}/{name}.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) { existing.color = color; return existing; }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var material = new Material(shader) { name = name, color = color };
        AssetDatabase.CreateAsset(material, path);
        return material;
    }
}
