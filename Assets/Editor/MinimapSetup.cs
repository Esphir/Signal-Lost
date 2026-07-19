using System;
using Signal.Generation;
using Signal.Minimap;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Signal.MinimapEditor
{
    /// <summary>
    /// One-click scaffold for the minimap: a screen-space Canvas with a masked, top-right viewport
    /// carrying the <see cref="MinimapManager"/>, plus a default <see cref="MinimapDatabase"/> pre-filled
    /// with Unity's built-in UI sprite and a distinct tint per room type, so it reads immediately and you
    /// swap in real art later. Everything the manager needs is wired; nothing is placed by hand.
    /// </summary>
    public static class MinimapSetup
    {
        private const string DatabasePath = "Assets/Scripts/Minimap/MinimapDatabase.asset";

        [MenuItem("Tools/Signal Lost/Minimap/Create Minimap")]
        public static void CreateMinimap()
        {
            MinimapDatabase db = CreateOrLoadDatabase();

            // Replace any existing minimap so re-running is clean (e.g. to pick up new default sizes).
            foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                if (root.name == "Minimap Canvas") Undo.DestroyObjectImmediate(root);

            var canvasGo = new GameObject("Minimap Canvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;

            // Framed viewport, anchored top-right, clipping anything that scrolls out of view.
            var container = new GameObject("Minimap",
                typeof(RectTransform), typeof(RectMask2D), typeof(MinimapManager));
            var crt = container.GetComponent<RectTransform>();
            crt.SetParent(canvasGo.transform, false);
            crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(1f, 1f);
            crt.sizeDelta = new Vector2(300f, 300f);
            crt.anchoredPosition = new Vector2(-12f, -12f);

            // Tiles + connections live here; the manager slides it to keep the current room centred.
            var content = new GameObject("Content", typeof(RectTransform));
            var conRt = content.GetComponent<RectTransform>();
            conRt.SetParent(crt, false);
            conRt.anchorMin = conRt.anchorMax = conRt.pivot = new Vector2(0.5f, 0.5f);
            conRt.sizeDelta = Vector2.zero;

            var manager = container.GetComponent<MinimapManager>();
            var so = new SerializedObject(manager);
            so.FindProperty("database").objectReferenceValue = db;
            so.FindProperty("content").objectReferenceValue = conRt;
            so.FindProperty("container").objectReferenceValue = crt;
            so.FindProperty("generator").objectReferenceValue = UnityEngine.Object.FindFirstObjectByType<LevelGenerator>();
            so.ApplyModifiedPropertiesWithoutUndo();

            Undo.RegisterCreatedObjectUndo(canvasGo, "Create Minimap");
            Selection.activeGameObject = container;
            EditorGUIUtility.PingObject(db);

            Debug.Log("[Minimap] Created 'Minimap Canvas'. It works as-is; assign your own sprites in " +
                      $"{DatabasePath} to reskin it.", manager);
            EditorUtility.DisplayDialog("Minimap",
                "Created a Minimap Canvas wired to the level generator.\n\nPress Play — rooms reveal as " +
                "you explore. Swap in real sprites via the MinimapDatabase asset whenever you like.", "OK");
        }

        private static MinimapDatabase CreateOrLoadDatabase()
        {
            var existing = AssetDatabase.LoadAssetAtPath<MinimapDatabase>(DatabasePath);
            if (existing != null) return existing;

            var db = ScriptableObject.CreateInstance<MinimapDatabase>();
            Sprite ui = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            db.unknownRoom = ui;
            db.discoveredRoom = ui;
            db.visitedRoom = ui;
            db.currentRoom = ui;
            db.connection = ui;
            // Border and current-room indicator are left empty: the built-in square would just cover the
            // tile. The current room already reads via scale + full brightness (+ pulse). Drop in a frame
            // and a glow/ring sprite here whenever you want them.

            // One icon entry per existing room type, sprite = the built-in square tinted per type, so the
            // map is colour-coded out of the box. Shop/Secret are future RoomType values — add them here
            // once the enum has them, no code change to the minimap itself.
            var so = new SerializedObject(db);
            SerializedProperty list = so.FindProperty("icons");
            var types = (RoomType[])Enum.GetValues(typeof(RoomType));
            list.arraySize = types.Length;
            for (int i = 0; i < types.Length; i++)
            {
                SerializedProperty e = list.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("type").enumValueIndex = (int)types[i];
                e.FindPropertyRelative("sprite").objectReferenceValue = ui;
                e.FindPropertyRelative("tint").colorValue = ColorFor(types[i]);
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.CreateAsset(db, DatabasePath);
            AssetDatabase.SaveAssets();
            return db;
        }

        private static Color ColorFor(RoomType type) => type switch
        {
            RoomType.Start => new Color(0.40f, 1.00f, 0.55f),
            RoomType.Combat => new Color(1.00f, 0.42f, 0.42f),
            RoomType.Treasure => new Color(1.00f, 0.85f, 0.30f),
            RoomType.Platforming => new Color(0.40f, 0.70f, 1.00f),
            RoomType.Checkpoint => new Color(0.45f, 1.00f, 1.00f),
            RoomType.Transition => new Color(0.65f, 0.65f, 0.68f),
            RoomType.Boss => new Color(1.00f, 0.30f, 0.60f),
            RoomType.End => new Color(0.70f, 0.42f, 1.00f),
            _ => Color.white,
        };
    }
}
