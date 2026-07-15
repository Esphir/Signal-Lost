using System;
using System.Collections.Generic;
using Signal.Loot;
using Signal.Run;
using Signal.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Signal.Dev
{
    /// <summary>
    /// Temporary developer/testing menu. Self-gating: it destroys itself unless
    /// <see cref="DeveloperModeManager.DeveloperMode"/> is on, so it can always be instantiated and
    /// leaves normal play untouched. F1 (or gamepad Back+Start) toggles it; while open it pauses the
    /// game and frees the cursor. Delegates each tab to a dedicated component, and drives the Run /
    /// Loot testing itself through the existing RunManager and loot systems.
    ///
    /// To remove entirely: delete the Assets/Scripts/Dev folder + the DeveloperMenu prefab, and drop
    /// its entry from the scene's SystemsBootstrap list. No gameplay system depends on it.
    /// </summary>
    public class DeveloperMenu : MonoBehaviour
    {
        [SerializeField] private LootSettingsSO lootSettings;
        [SerializeField, Min(0.5f)] private float lootSpawnDistance = 3f;

        private static readonly Color PanelColor = new Color(0.09f, 0.09f, 0.13f, 0.97f);
        private static readonly Color ButtonColor = new Color(0.16f, 0.16f, 0.22f);
        private static readonly Color ActiveTabColor = new Color(0.27f, 0.27f, 0.38f);
        private static readonly string[] TabNames = { "Player", "Enemies", "Run", "Debug" };

        private StatEditor _statEditor;
        private EnemySpawner _enemySpawner;
        private DebugInfoPanel _debugInfo;

        private GameObject _overlay;
        private RectTransform _content;
        private readonly Button[] _tabButtons = new Button[4];
        private readonly List<Action> _refreshers = new List<Action>();
        private int _activeTab;
        private ItemRarity _lootRarity = ItemRarity.Common;

        private PlayerInput _playerInput;
        private CursorLockMode _previousLock;
        private bool _previousCursorVisible;

        public bool IsOpen => _overlay != null;

        /// <summary>Closes the menu (and unpauses) if open — used by actions that hand control back to gameplay.</summary>
        public void CloseMenu()
        {
            if (IsOpen) Close();
        }

        private void Awake()
        {
            if (!DeveloperModeManager.DeveloperMode)
            {
                Destroy(gameObject);
                return;
            }
            _statEditor = GetComponent<StatEditor>();
            _enemySpawner = GetComponent<EnemySpawner>();
            _debugInfo = GetComponent<DebugInfoPanel>();
        }

        private void Update()
        {
            if (TogglePressedThisFrame())
            {
                if (IsOpen) Close();
                else Open();
            }

            if (!IsOpen) return;
            for (int i = 0; i < _refreshers.Count; i++) _refreshers[i]?.Invoke();
        }

        private static bool TogglePressedThisFrame()
        {
            if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame) return true;
            return Gamepad.current != null
                && Gamepad.current.selectButton.isPressed
                && Gamepad.current.startButton.wasPressedThisFrame;
        }

        private void Open()
        {
            UiBuilder.EnsureEventSystem();
            _previousLock = Cursor.lockState;
            _previousCursorVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Time.timeScale = 0f;

            _playerInput = ResolvePlayerInput();
            if (_playerInput != null) _playerInput.DeactivateInput();

            UiModalState.Push();
            BuildOverlay();
            ShowTab(_activeTab);
        }

        private void Close()
        {
            if (_overlay != null) Destroy(_overlay);
            _overlay = null;
            _refreshers.Clear();

            Time.timeScale = 1f;
            Cursor.lockState = _previousLock;
            Cursor.visible = _previousCursorVisible;
            if (_playerInput != null) _playerInput.ActivateInput();
            UiModalState.Pop();
        }

        private void OnDestroy()
        {
            if (IsOpen) Close();
        }

        // ── Shell ─────────────────────────────────────────────────────────────

        private void BuildOverlay()
        {
            Canvas canvas = UiBuilder.CreateOverlayCanvas("DeveloperCanvas", 80);
            _overlay = canvas.gameObject;

            Image panel = UiBuilder.NewChild<Image>(canvas.transform, "Panel");
            panel.color = PanelColor;
            panel.rectTransform.sizeDelta = new Vector2(900f, 820f);

            Text title = UiBuilder.CreateText(panel.transform, "Title", "DEVELOPER MENU", 30, FontStyle.Bold, TextAnchor.MiddleCenter);
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.anchoredPosition = new Vector2(0f, -32f);
            title.rectTransform.sizeDelta = new Vector2(0f, 50f);

            var tabs = UiBuilder.NewChild<HorizontalLayoutGroup>(panel.transform, "Tabs");
            tabs.spacing = 8f;
            tabs.childAlignment = TextAnchor.MiddleCenter;
            tabs.childControlWidth = false;
            tabs.childControlHeight = false;
            var tabsRect = (RectTransform)tabs.transform;
            tabsRect.anchorMin = new Vector2(0f, 1f);
            tabsRect.anchorMax = new Vector2(1f, 1f);
            tabsRect.anchoredPosition = new Vector2(0f, -80f);
            tabsRect.sizeDelta = new Vector2(-40f, 44f);

            for (int i = 0; i < TabNames.Length; i++)
            {
                int index = i;
                Button tab = UiBuilder.CreateButton(tabs.transform, $"Tab_{TabNames[i]}", TabNames[i], ButtonColor, 20, out _);
                ((RectTransform)tab.transform).sizeDelta = new Vector2(150f, 40f);
                tab.onClick.AddListener(() => ShowTab(index));
                _tabButtons[i] = tab;
            }

            Button close = UiBuilder.CreateButton(tabs.transform, "CloseTab", "Close (F1)", ButtonColor, 20, out _);
            ((RectTransform)close.transform).sizeDelta = new Vector2(150f, 40f);
            close.onClick.AddListener(Close);

            ScrollRect scroll = UiBuilder.CreateScrollView(panel.transform, "Scroll", out RectTransform content);
            var scrollRect = (RectTransform)scroll.transform;
            scrollRect.anchorMin = new Vector2(0f, 0f);
            scrollRect.anchorMax = new Vector2(1f, 1f);
            scrollRect.offsetMin = new Vector2(20f, 20f);
            scrollRect.offsetMax = new Vector2(-20f, -110f);
            _content = content;

            EventSystem.current.SetSelectedGameObject(_tabButtons[0].gameObject);
        }

        private void ShowTab(int index)
        {
            _activeTab = index;
            _refreshers.Clear();
            for (int i = _content.childCount - 1; i >= 0; i--)
                Destroy(_content.GetChild(i).gameObject);

            for (int i = 0; i < _tabButtons.Length; i++)
                if (_tabButtons[i] != null)
                    _tabButtons[i].image.color = i == index ? ActiveTabColor : ButtonColor;

            switch (index)
            {
                case 0: _statEditor.BuildPlayerTab(_content, _refreshers); break;
                case 1: _enemySpawner.BuildEnemiesTab(_content); break;
                case 2: BuildRunTab(_content); break;
                case 3: _debugInfo.BuildDebugTab(_content, _refreshers); break;
            }
        }

        // ── Run / Loot tab ────────────────────────────────────────────────────

        private void BuildRunTab(Transform parent)
        {
            AddInfoRow(parent, "Enemies Killed", () => Stats.EnemiesKilled.ToString());
            AddInfoRow(parent, "Loot Dropped", () => Stats.LootDropped.ToString());
            AddInfoRow(parent, "Loot Collected", () => Stats.LootCollected.ToString());
            AddInfoRow(parent, "Current Run Time", () => FormatTime(Stats.Duration));
            AddInfoRow(parent, "Current Run Upgrades", () => RunManager.HasInstance ? RunManager.Instance.Data.Upgrades.Count.ToString() : "0");
            AddInfoRow(parent, "Highest Rarity", () => Stats.HasCollectedLoot ? Stats.HighestRarity.ToString() : "—");

            AddButton(parent, "End Run", EndRun);
            AddButton(parent, "Clear All Enemies", ClearAllEnemies);

            AddInfoRow(parent, "Loot Rarity", () => _lootRarity.ToString(), "◄", "►",
                () => CycleRarity(-1), () => CycleRarity(1));
            AddButton(parent, "Spawn Loot", () => SpawnLoot(_lootRarity));
            AddButton(parent, "Force Legendary Loot", () => SpawnLoot(ItemRarity.Legendary));
            AddButton(parent, "Force Common Loot", () => SpawnLoot(ItemRarity.Common));
        }

        private static RunStats Stats => RunManager.HasInstance ? RunManager.Instance.Statistics : default;

        private void EndRun()
        {
            Close();
            if (RunManager.HasInstance) RunManager.Instance.EndRun(RunEndReason.Victory);
        }

        private void ClearAllEnemies()
        {
            foreach (GameObject enemy in GameObject.FindGameObjectsWithTag("Enemy"))
                Destroy(enemy);
        }

        private void CycleRarity(int delta)
            => _lootRarity = (ItemRarity)Mathf.Clamp((int)_lootRarity + delta, 0, 4);

        private void SpawnLoot(ItemRarity rarity)
        {
            if (lootSettings == null || lootSettings.lootPrefab == null)
            {
                Debug.LogWarning("[Dev] DeveloperMenu has no LootSettings (with a loot prefab) assigned.", this);
                return;
            }
            GameObject player = GameObject.FindWithTag("Player");
            if (player == null) return;

            Vector3 forward = player.transform.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.forward;
            Vector3 pos = player.transform.position + forward * lootSpawnDistance + Vector3.up * 0.5f;

            LootPickup loot = LootPool.Spawn(lootSettings.lootPrefab, pos, Quaternion.identity);
            if (loot != null) loot.Initialize(rarity, lootSettings);
        }

        // ── Small builders ────────────────────────────────────────────────────

        private void AddInfoRow(Transform parent, string label, Func<string> read,
            string minusLabel = null, string plusLabel = null, Action onMinus = null, Action onPlus = null)
        {
            Image row = UiBuilder.NewChild<Image>(parent, $"Info_{label}");
            row.color = new Color(1f, 1f, 1f, 0.04f);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 38f;

            var hl = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.padding = new RectOffset(10, 10, 4, 4);
            hl.spacing = 8f;
            hl.childAlignment = TextAnchor.MiddleLeft;
            hl.childControlWidth = true;
            hl.childForceExpandWidth = false;
            hl.childControlHeight = true;
            hl.childForceExpandHeight = true;

            UiBuilder.CreateText(row.transform, "Label", label, 18, FontStyle.Normal, TextAnchor.MiddleLeft)
                .gameObject.AddComponent<LayoutElement>().preferredWidth = 260f;
            Text value = UiBuilder.CreateText(row.transform, "Value", read(), 18, FontStyle.Bold, TextAnchor.MiddleLeft);
            value.gameObject.AddComponent<LayoutElement>().preferredWidth = 200f;

            if (onMinus != null) AddInlineButton(row.transform, minusLabel, onMinus);
            if (onPlus != null) AddInlineButton(row.transform, plusLabel, onPlus);

            _refreshers.Add(() => value.text = read());
        }

        private void AddInlineButton(Transform parent, string label, Action onClick)
        {
            Button button = UiBuilder.CreateButton(parent, $"Btn_{label}", label, ButtonColor, 20, out _);
            button.gameObject.AddComponent<LayoutElement>().preferredWidth = 46f;
            button.onClick.AddListener(() => onClick?.Invoke());
        }

        private void AddButton(Transform parent, string label, Action onClick)
        {
            Button button = UiBuilder.CreateButton(parent, $"Run_{label}", label, ButtonColor, 18, out _);
            button.gameObject.AddComponent<LayoutElement>().preferredHeight = 38f;
            button.onClick.AddListener(() => onClick?.Invoke());
        }

        private static string FormatTime(float seconds)
        {
            int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return $"{total / 60}:{total % 60:00}";
        }

        private static PlayerInput ResolvePlayerInput()
        {
            GameObject player = GameObject.FindWithTag("Player");
            return player != null ? player.GetComponent<PlayerInput>() : null;
        }
    }
}
