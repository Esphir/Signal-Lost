using Signal.Generation;
using UnityEditor;
using UnityEngine;

namespace Signal.GenerationEditor
{
    /// <summary>
    /// Regenerate / Clear buttons plus a read-out of the last run. Works in Edit Mode too, so a
    /// designer can eyeball layouts without entering Play.
    /// </summary>
    [CustomEditor(typeof(LevelGenerator))]
    public class LevelGeneratorEditor : Editor
    {
        private const string SeedPrefKey = "SignalLost.LevelGenerator.ReproSeed";

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var generator = (LevelGenerator)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Generation", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Regenerate")) generator.Generate();
                if (GUILayout.Button("Clear")) generator.Clear();
            }

            // Reproduce a specific layout by seed — the fast way to re-check a seed that misbehaved.
            // Feeds LevelGenerator.PendingSeed, which Generate() consumes exactly like the save/resume flow.
            int seed = EditorPrefs.GetInt(SeedPrefKey, 12345);
            using (new EditorGUILayout.HorizontalScope())
            {
                int edited = EditorGUILayout.IntField("Seed", seed);
                if (edited != seed) EditorPrefs.SetInt(SeedPrefKey, edited);
                if (GUILayout.Button("Regenerate With Seed", GUILayout.MaxWidth(170f)))
                {
                    LevelGenerator.PendingSeed = edited;
                    generator.Generate();
                }
            }

            if (GUILayout.Button("Validate Rooms"))
                RoomAuthoringTools.Validate();

            if (generator.Rooms.Count == 0)
            {
                EditorGUILayout.HelpBox("No level generated. Press Regenerate, or press Play.", MessageType.Info);
                return;
            }

            // The seed is the useful artefact here: it's how a designer keeps a layout they liked.
            EditorGUILayout.LabelField("Last Seed", generator.LastSeed.ToString());
            if (GUILayout.Button("Copy Seed To Clipboard")) EditorGUIUtility.systemCopyBuffer = generator.LastSeed.ToString();

            GenerationReport report = generator.LastReport;
            if (report == null) return;

            EditorGUILayout.LabelField("Result", report.ToString());
            if (report.Problems.Count > 0)
                EditorGUILayout.HelpBox(string.Join("\n", report.Problems), MessageType.Warning);
            else
                EditorGUILayout.HelpBox("No overlaps, no disconnected rooms.", MessageType.Info);
        }
    }
}
