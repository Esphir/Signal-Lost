using System.Collections.Generic;
using System.Linq;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// One-shot scaffold: fills "Level 1" with the standard gameplay essentials, mirroring how the
/// Test/Tutorial scenes are assembled. Safe to re-run — it rebuilds the managed objects each time.
/// </summary>
public static class BuildLevel1Scene
{
    private const string ScenePath = "Assets/Scenes/Level 1.unity";

    private const string PlayerPrefab = "Assets/Prefabs/Player.prefab";
    private const string MainCameraPrefab = "Assets/Prefabs/Camera/Main Camera.prefab";
    private const string CinemachinePrefab = "Assets/Prefabs/Camera/CinemachineCamera.prefab";
    private const string GameSystemsPrefab = "Assets/Prefabs/UI/GameSystems.prefab";
    private const string GameplayHudPrefab = "Assets/Prefabs/UI/GameplayHUD.prefab";
    private const string VolumeProfile = "Assets/Settings/SampleSceneProfile.asset";

    private static readonly Vector3 PlayerSpawn = new Vector3(0f, 1f, 0f);
    private static readonly Vector3 FloorSize = new Vector3(60f, 1f, 60f);

    [MenuItem("Tools/Signal Lost/Build Level 1 Scene")]
    public static void Build()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        // The stock camera carries no CinemachineBrain; the prefab does. Removing it also avoids a
        // second AudioListener once the prefab lands.
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.GetComponent<Camera>() != null && PrefabUtility.GetCorrespondingObjectFromSource(root) == null)
                Object.DestroyImmediate(root);

        RemoveExisting(scene, "Main Camera", "CinemachineCamera", "Player", "GameSystems",
            "GameplayHUD", "Global Volume", "Floor");

        GameObject player = Place(PlayerPrefab, PlayerSpawn, Quaternion.identity);
        Place(MainCameraPrefab, new Vector3(0f, 3f, -8f), Quaternion.Euler(15f, 0f, 0f));
        GameObject cmCamera = Place(CinemachinePrefab, new Vector3(0f, 3f, -8f), Quaternion.Euler(15f, 0f, 0f));
        Place(GameSystemsPrefab, Vector3.zero, Quaternion.identity);
        Place(GameplayHudPrefab, Vector3.zero, Quaternion.identity);

        // Cinemachine drives the Main Camera via the brain; without a tracking target it never follows.
        var vcam = cmCamera.GetComponent<CinemachineCamera>();
        vcam.Target.TrackingTarget = player.transform;
        EditorUtility.SetDirty(vcam);

        CreateFloor();
        CreateGlobalVolume();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);

        RegisterInBuildSettings();

        Debug.Log($"[Level 1] Built: {string.Join(", ", scene.GetRootGameObjects().Select(g => g.name))}");
    }

    private static void RemoveExisting(Scene scene, params string[] names)
    {
        var wanted = new HashSet<string>(names);
        foreach (GameObject root in scene.GetRootGameObjects())
            if (wanted.Contains(root.name))
                Object.DestroyImmediate(root);
    }

    private static GameObject Place(string assetPath, Vector3 position, Quaternion rotation)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (prefab == null) throw new System.IO.FileNotFoundException($"Missing prefab: {assetPath}");

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.transform.SetPositionAndRotation(position, rotation);
        instance.name = prefab.name;
        return instance;
    }

    /// <summary>Ground-layer box so PlayerController's ground check (mask = Ground) sees it.</summary>
    private static void CreateFloor()
    {
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Floor";
        floor.layer = LayerMask.NameToLayer("Ground");
        floor.isStatic = true;
        // Scaled so the walkable surface sits at y = 0.
        floor.transform.localPosition = new Vector3(0f, -FloorSize.y * 0.5f, 0f);
        floor.transform.localScale = FloorSize;
    }

    private static void CreateGlobalVolume()
    {
        var go = new GameObject("Global Volume");
        var volume = go.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfile);
    }

    private static void RegisterInBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.Any(s => s.path == ScenePath)) return;

        scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
