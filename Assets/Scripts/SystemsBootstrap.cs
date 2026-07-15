using UnityEngine;

/// <summary>
/// Instantiates gameplay-system prefabs (pause menu, run-end screen, …) when a gameplay scene
/// loads, so each scene only needs this one object rather than every system placed by hand.
/// </summary>
public class SystemsBootstrap : MonoBehaviour
{
    [SerializeField]
    [Tooltip("System prefabs to instantiate on startup.")]
    private GameObject[] prefabs;

    private void Awake()
    {
        if (prefabs == null) return;
        foreach (GameObject prefab in prefabs)
            if (prefab != null) Instantiate(prefab);
    }
}
