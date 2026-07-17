using Signal.Spawning;
using UnityEditor;
using UnityEngine;

namespace Signal.SpawningEditor
{
    /// <summary>
    /// Adds Spawn / Reset buttons to the section Inspector. Both are gated to Play Mode on purpose:
    /// spawning at edit time would instantiate enemies straight into the saved scene.
    /// </summary>
    [CustomEditor(typeof(EnemySpawnSection))]
    public class EnemySpawnSectionEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var section = (EnemySpawnSection)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Debug", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to spawn or reset this section.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Status", section.HasSpawned
                ? $"Spawned — {section.SpawnedEnemies.Count} enemies"
                : "Waiting for the player");

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(section.HasSpawned))
                    if (GUILayout.Button("Spawn Now")) section.Activate();

                if (GUILayout.Button("Reset")) section.ResetSection();
            }
        }
    }
}
