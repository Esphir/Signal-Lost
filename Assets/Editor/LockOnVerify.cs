using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>TEMPORARY scaffold: play-tests the lock-on indicator + retargeting. Deleted after use.</summary>
public static class LockOnVerify
{
    [MenuItem("Tools/Signal Lost/Verify Lock-On")]
    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("LockOnVerifyRunner").AddComponent<LockOnVerifyRunner>();

        Debug.Log("[LockOn] Entering play mode…");
        EditorApplication.EnterPlaymode();
    }
}
