using System.Collections.Generic;
using System.IO;
using System.Linq;
using Signal.Audio;
using Signal.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

/// <summary>
/// One-shot scaffold: wires the audio system into the shared prefabs (player, enemies, sewage) and
/// into Level 1 as the reference scene. Idempotent — re-running reconfigures rather than duplicates.
/// </summary>
public static class BuildLevel1Audio
{
    private const string ScenePath = "Assets/Scenes/Level 1.unity";
    private const string CueRoot = "Assets/Scripts/Audio/Database/Cues";
    private const string ChannelPath = "Assets/Scripts/Audio/Events/AudioEventChannel.asset";
    private const string DatabasePath = "Assets/Scripts/Audio/Database/AudioDatabase.asset";
    private const string AudioSystemPrefabPath = "Assets/Prefabs/UI/AudioSystem.prefab";
    private const string MixerPath = "Assets/Settings/GameMixer.mixer";

    private static AudioEventChannel _channel;
    private static AudioMixerGroup _sfxGroup;
    private static AudioMixerGroup _uiGroup;

    [MenuItem("Tools/Signal Lost/Build Level 1 Audio")]
    public static void Build()
    {
        _channel = Load<AudioEventChannel>(ChannelPath);
        LoadMixerGroups();

        BuildAudioSystemPrefab();
        ConfigurePlayerPrefab();
        ConfigureEnemyPrefabs();
        ConfigureSewagePrefab();
        ConfigureScene();

        AssetDatabase.SaveAssets();
        Debug.Log("[AudioWire] Done.");
    }

    // ── Mixer ─────────────────────────────────────────────────────────────────

    private static void LoadMixerGroups()
    {
        var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
        if (mixer == null)
        {
            Debug.LogWarning($"[AudioWire] No mixer at {MixerPath} — cues will route to the default output. " +
                             "Create one and re-run to wire groups.");
            return;
        }

        // Child groups can only be authored in the Mixer window, so fall back to Master when the
        // dedicated groups don't exist yet. Re-running picks them up automatically once they do.
        AudioMixerGroup master = mixer.FindMatchingGroups("Master").FirstOrDefault();
        _sfxGroup = mixer.FindMatchingGroups("SFX").FirstOrDefault() ?? master;
        _uiGroup = mixer.FindMatchingGroups("UI").FirstOrDefault() ?? master;

        Debug.Log($"[AudioWire] Mixer '{mixer.name}': SFX -> {(_sfxGroup != null ? _sfxGroup.name : "none")}, " +
                  $"UI -> {(_uiGroup != null ? _uiGroup.name : "none")}" +
                  $"{(_sfxGroup == master ? "  (no SFX/UI child groups found; routed to Master)" : "")}");

        // Route every cue to the group matching its category.
        foreach (string guid in AssetDatabase.FindAssets("t:AudioCue", new[] { CueRoot }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var cue = AssetDatabase.LoadAssetAtPath<AudioCue>(path);
            if (cue == null) continue;

            bool isUi = path.Contains("/UI/") || path.Contains("/Tutorial/") || path.EndsWith("/Loot/Pickup.asset")
                        || path.EndsWith("/Loot/StatSelected.asset");
            AudioMixerGroup group = isUi ? _uiGroup : _sfxGroup;
            if (group == null) continue;

            var so = new SerializedObject(cue);
            so.FindProperty("mixerGroup").objectReferenceValue = group;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    // ── Audio system prefab (manager + UI controller) ─────────────────────────

    private static void BuildAudioSystemPrefab()
    {
        var root = new GameObject("AudioSystem");

        var manager = root.AddComponent<AudioManager>();
        var mso = new SerializedObject(manager);
        SetArray(mso.FindProperty("channels"), new Object[] { _channel });
        mso.FindProperty("database").objectReferenceValue = Load<AudioDatabase>(DatabasePath);
        mso.FindProperty("defaultSfxGroup").objectReferenceValue = _sfxGroup;
        mso.FindProperty("masterVolume").floatValue = 1f;
        mso.FindProperty("sfxVolume").floatValue = 1f;
        mso.FindProperty("poolSize").intValue = 24;
        mso.FindProperty("maxSimultaneousSounds").intValue = 48;
        mso.ApplyModifiedPropertiesWithoutUndo();

        // UI audio lives with the manager so any scene that has audio has menu feedback.
        var uiGo = new GameObject("UIAudio");
        uiGo.transform.SetParent(root.transform, false);
        WireEmitter(uiGo);
        var ui = uiGo.AddComponent<UIAudioController>();
        var uso = new SerializedObject(ui);
        Assign(uso, "hover", "UI/Hover");
        Assign(uso, "click", "UI/Click");
        Assign(uso, "confirm", "UI/Confirm");
        Assign(uso, "cancel", "UI/Cancel");
        Assign(uso, "error", "UI/Error");
        Assign(uso, "pause", "UI/Pause");
        Assign(uso, "resume", "UI/Resume");
        Assign(uso, "menuOpen", "UI/MenuOpen");
        Assign(uso, "menuClose", "UI/MenuClose");
        Assign(uso, "lootPickup", "Loot/Pickup");
        Assign(uso, "statSelected", "Loot/StatSelected");
        uso.ApplyModifiedPropertiesWithoutUndo();

        Directory.CreateDirectory(Path.GetDirectoryName(AudioSystemPrefabPath));
        PrefabUtility.SaveAsPrefabAsset(root, AudioSystemPrefabPath);
        Object.DestroyImmediate(root);
        Debug.Log($"[AudioWire] AudioSystem prefab -> {AudioSystemPrefabPath}");
    }

    // ── Player ────────────────────────────────────────────────────────────────

    private static void ConfigurePlayerPrefab()
    {
        using var scope = new PrefabScope("Assets/Prefabs/Player.prefab");
        WireEmitter(scope.Root);

        var controller = GetOrAdd<PlayerAudioController>(scope.Root);
        var so = new SerializedObject(controller);
        Assign(so, "footstep", "Player/Footstep");
        Assign(so, "jump", "Player/Jump");
        Assign(so, "doubleJump", "Player/DoubleJump");
        Assign(so, "land", "Player/Land");
        Assign(so, "dodge", "Player/Dodge");
        Assign(so, "lightAttack", "Player/LightAttack");
        Assign(so, "heavyAttack", "Player/HeavyAttack");
        Assign(so, "bash", "Player/Bash");
        Assign(so, "attackHit", "Player/AttackHit");
        Assign(so, "hit", "Player/Hit");
        Assign(so, "death", "Player/Death");
        Assign(so, "respawn", "Player/Respawn");
        Assign(so, "heal", "Player/Heal");
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ── Enemies ───────────────────────────────────────────────────────────────

    private static void ConfigureEnemyPrefabs()
    {
        // TrainingDummy/BashDummy stand in for the not-yet-existing Basic Enemy: common set only.
        ConfigureEnemy<EnemyAudioController>("Assets/Prefabs/Enemies/TrainingDummy.prefab", null);
        ConfigureEnemy<EnemyAudioController>("Assets/Prefabs/Enemies/BashDummy.prefab", null);

        ConfigureEnemy<LobberAudioController>("Assets/Prefabs/Enemies/Lober.prefab", so =>
        {
            Assign(so, "throwCue", "Lobber/Throw");
            Assign(so, "explosion", "Lobber/Explosion");
        });

        ConfigureEnemy<PlummeterAudioController>("Assets/Prefabs/Enemies/Plummeter.prefab", so =>
        {
            Assign(so, "leap", "Plummeter/Leap");
            Assign(so, "falling", "Plummeter/Falling");
            Assign(so, "slam", "Plummeter/Slam");
        });

        ConfigureEnemy<SupportAudioController>("Assets/Prefabs/Enemies/Supporter.prefab", so =>
        {
            Assign(so, "buffCast", "Support/BuffCast");
            Assign(so, "buffApplied", "Support/BuffApplied");
        });
    }

    /// <summary>Common enemy cues come from one place — no per-enemy duplication.</summary>
    private static void ConfigureEnemy<T>(string path, System.Action<SerializedObject> extra)
        where T : EnemyAudioController
    {
        using var scope = new PrefabScope(path);
        WireEmitter(scope.Root);

        // Drop any controller of the wrong type left by an earlier run.
        foreach (EnemyAudioController existing in scope.Root.GetComponents<EnemyAudioController>())
            if (existing.GetType() != typeof(T)) Object.DestroyImmediate(existing, true);

        var controller = GetOrAdd<T>(scope.Root);
        var so = new SerializedObject(controller);
        Assign(so, "spawn", "Enemy/Spawn");
        Assign(so, "attack", "Enemy/Attack");
        Assign(so, "hit", "Enemy/Hit");
        Assign(so, "death", "Enemy/Death");
        Assign(so, "stunned", "Enemy/Stunned");
        extra?.Invoke(so);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ── Hazards ───────────────────────────────────────────────────────────────

    private static void ConfigureSewagePrefab()
    {
        using var scope = new PrefabScope("Assets/Prefabs/Hazards/Sewage.prefab");
        WireEmitter(scope.Root);

        var controller = GetOrAdd<HazardAudioController>(scope.Root);
        var so = new SerializedObject(controller);
        Assign(so, "splash", "Hazard/SewageSplash");
        Assign(so, "respawn", "Hazard/Respawn");
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ── Scene ─────────────────────────────────────────────────────────────────

    private static void ConfigureScene()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.name is "AudioSystem" or "Checkpoint") Object.DestroyImmediate(root);

        var audioPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AudioSystemPrefabPath);
        PrefabUtility.InstantiatePrefab(audioPrefab);

        // A checkpoint so the environment cues are demonstrated and respawn has a real target.
        var checkpoint = new GameObject("Checkpoint");
        checkpoint.transform.position = new Vector3(0f, 0f, 4f);
        checkpoint.AddComponent<Checkpoint>();
        WireEmitter(checkpoint);
        var cpAudio = checkpoint.AddComponent<CheckpointAudioController>();
        var cso = new SerializedObject(cpAudio);
        Assign(cso, "activated", "Environment/CheckpointActivated");
        Assign(cso, "respawn", "Hazard/Respawn");
        cso.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log($"[AudioWire] Scene configured: {string.Join(", ", scene.GetRootGameObjects().Select(g => g.name))}");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Every emitter shares the one channel — that's the whole wiring contract.</summary>
    private static void WireEmitter(GameObject go)
    {
        var emitter = GetOrAdd<AudioEmitter>(go);
        var so = new SerializedObject(emitter);
        so.FindProperty("channel").objectReferenceValue = _channel;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        T existing = go.GetComponent<T>();
        return existing != null ? existing : go.AddComponent<T>();
    }

    private static void Assign(SerializedObject so, string field, string cueId)
    {
        SerializedProperty property = so.FindProperty(field);
        if (property == null) { Debug.LogError($"[AudioWire] No field '{field}' on {so.targetObject.GetType().Name}"); return; }

        var cue = AssetDatabase.LoadAssetAtPath<AudioCue>($"{CueRoot}/{cueId}.asset");
        if (cue == null) { Debug.LogError($"[AudioWire] Missing cue asset: {cueId}"); return; }

        property.objectReferenceValue = cue;
    }

    private static void SetArray(SerializedProperty property, Object[] values)
    {
        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    private static T Load<T>(string path) where T : Object
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null) Debug.LogError($"[AudioWire] MISSING: {path}");
        return asset;
    }

    /// <summary>Opens a prefab for editing and saves it back on dispose.</summary>
    private sealed class PrefabScope : System.IDisposable
    {
        private readonly string _path;
        public GameObject Root { get; }

        public PrefabScope(string path)
        {
            _path = path;
            Root = PrefabUtility.LoadPrefabContents(path);
        }

        public void Dispose()
        {
            PrefabUtility.SaveAsPrefabAsset(Root, _path);
            PrefabUtility.UnloadPrefabContents(Root);
            Debug.Log($"[AudioWire] Configured {Path.GetFileNameWithoutExtension(_path)}");
        }
    }
}
