// Gameplay HUD bootstrap: hosts (or creates) the HUD canvas and instantiates the player health bar prefab onto it.
using UnityEngine;

namespace Signal.UI
{
    public class GameplayHud : MonoBehaviour
    {
        [SerializeField] private PlayerHealthBarUI playerHealthBarPrefab;
        [SerializeField]
        [Tooltip("Canvas to place HUD elements on. Left empty, an overlay canvas is created.")]
        private Canvas targetCanvas;

        private void Awake()
        {
            Canvas canvas = targetCanvas != null ? targetCanvas : UiBuilder.CreateOverlayCanvas("GameplayHUDCanvas", 10);
            if (playerHealthBarPrefab != null)
                Instantiate(playerHealthBarPrefab, canvas.transform, false);
        }
    }
}
