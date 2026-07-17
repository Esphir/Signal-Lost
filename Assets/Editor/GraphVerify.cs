using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>TEMPORARY scaffold: play-tests the connector-graph generator in Level 1. Deleted after use.</summary>
public static class GraphVerify
{
    [MenuItem("Tools/Signal Lost/Verify Connector Graph")]
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Level 1.unity", OpenSceneMode.Single);
        new GameObject("GraphVerifyRunner").AddComponent<GraphVerifyRunner>();

        Debug.Log("[Graph] Entering play mode…");
        EditorApplication.EnterPlaymode();
    }
}
