// Regenerate / Clear buttons plus a read-out of the last run.
using Signal.Generation;
using UnityEditor;
using UnityEngine;

namespace Signal.GenerationEditor
{
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

            if (generator.Rooms.Count == 0)
            {
                EditorGUILayout.HelpBox("No level generated. Press Regenerate, or press Play.", MessageType.Info);
                return;
            }

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
