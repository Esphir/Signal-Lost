using System;
using System.Collections.Generic;
using Signal.Loot;
using Signal.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Signal.Dev
{
    /// <summary>
    /// Developer "Debug" tab: live scene name, smoothed FPS, enemy/loot counts, developer-mode
    /// state, and player position. All read-only, refreshed each frame the menu is open.
    /// </summary>
    public class DebugInfoPanel : MonoBehaviour
    {
        private float _smoothedFps;

        public void BuildDebugTab(Transform parent, List<Action> refreshers)
        {
            _smoothedFps = 0f;

            Text text = UiBuilder.CreateText(parent, "DebugInfo", "", 18, FontStyle.Normal, TextAnchor.UpperLeft);
            text.gameObject.AddComponent<LayoutElement>().preferredHeight = 220f;

            refreshers.Add(() => text.text = BuildText());
        }

        private string BuildText()
        {
            float dt = Time.unscaledDeltaTime;
            if (dt > 0f)
            {
                float fps = 1f / dt;
                _smoothedFps = _smoothedFps <= 0f ? fps : Mathf.Lerp(_smoothedFps, fps, 0.1f);
            }

            int enemies = GameObject.FindGameObjectsWithTag("Enemy").Length;
            int loot = FindObjectsByType<LootPickup>(FindObjectsSortMode.None).Length;
            GameObject player = GameObject.FindWithTag("Player");
            Vector3 pos = player != null ? player.transform.position : Vector3.zero;

            return
                $"Scene:  {SceneManager.GetActiveScene().name}\n" +
                $"FPS:  {_smoothedFps:0}\n" +
                $"Enemies:  {enemies}\n" +
                $"Active Loot:  {loot}\n" +
                $"Developer Mode:  {(DeveloperModeManager.DeveloperMode ? "ON" : "OFF")}\n" +
                $"Player Pos:  {pos.x:0.0}, {pos.y:0.0}, {pos.z:0.0}";
        }
    }
}
