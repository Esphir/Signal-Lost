// One-shot scaffold: turns Level 1 into the procedural reference scene — the hand-built floor and spawn sections give way to a LevelGenerator that builds the level on Play.
using System.Linq;
using Signal.Generation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BuildLevel1Generation
{
    private const string ScenePath = "Assets/Scenes/Level 1.unity";

    [MenuItem("Tools/Signal Lost/Build Level 1 Generation")]
    public static void Build()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        string[] retired = { "Floor", "Spawn Section A", "Spawn Section B", "Checkpoint", "LevelGenerator" };
        foreach (GameObject root in scene.GetRootGameObjects())
            if (retired.Contains(root.name)) Object.DestroyImmediate(root);

        var go = new GameObject("LevelGenerator");
        go.transform.position = Vector3.zero;
        var generator = go.AddComponent<LevelGenerator>();

        var so = new SerializedObject(generator);
        so.FindProperty("database").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<RoomDatabase>("Assets/Scripts/Generation/RoomDatabase.asset");
        so.FindProperty("settings").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<GenerationSettings>("Assets/Scripts/Generation/GenerationSettings.asset");
        so.FindProperty("generateOnAwake").boolValue = true;
        so.FindProperty("movePlayerToStart").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);

        Debug.Log($"[Gen1] Level 1 roots: {string.Join(", ", scene.GetRootGameObjects().Select(g => g.name))}");
    }
}
