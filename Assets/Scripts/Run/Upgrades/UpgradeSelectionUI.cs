using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Signal.Run.Upgrades
{
    /// <summary>
    /// The "choose one of three upgrades" overlay shown when loot is collected. Built from code so
    /// no prefab or scene canvas is needed. Optionally pauses gameplay while open. Picking a card
    /// applies the upgrade to the current run immediately and closes the overlay.
    /// </summary>
    public class UpgradeSelectionUI : MonoBehaviour
    {
        [SerializeField] private UpgradeTableSO upgradeTable;
        [SerializeField, Min(1)] private int choiceCount = 3;
        [SerializeField]
        [Tooltip("Freeze gameplay (timeScale 0) while the choice is on screen.")]
        private bool pauseDuringChoice = true;
        [SerializeField]
        [Tooltip("Press U to fake a loot pickup of a random rarity, for testing.")]
        private bool debugHotkey = true;

        private static readonly Color CardColor = new Color(0.13f, 0.13f, 0.17f, 0.95f);
        private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.6f);

        private readonly List<UpgradeOption> _options = new List<UpgradeOption>();
        private GameObject _overlay;
        private Font _font;
        private CursorLockMode _previousLock;
        private bool _previousCursorVisible;

        public bool IsOpen => _overlay != null;

        private void Update()
        {
            if (debugHotkey && !IsOpen && Keyboard.current != null && Keyboard.current.uKey.wasPressedThisFrame)
                Open((ItemRarity)Random.Range(0, 5), Color.white);

            // Guard against other systems (hit-stop) restoring timeScale mid-choice.
            if (IsOpen && pauseDuringChoice && Time.timeScale != 0f)
                Time.timeScale = 0f;
        }

        /// <summary>Rolls the choices for the given rarity and shows the overlay.</summary>
        public void Open(ItemRarity rarity, Color accent)
        {
            if (IsOpen) return;
            if (upgradeTable == null)
            {
                Debug.LogWarning("[Run] UpgradeSelectionUI has no upgrade table assigned.", this);
                return;
            }

            upgradeTable.GetRandomOptions(rarity, choiceCount, _options);
            if (_options.Count == 0)
            {
                Debug.LogWarning("[Run] Upgrade table produced no options.", this);
                return;
            }

            EnsureEventSystem();
            _previousLock = Cursor.lockState;
            _previousCursorVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (pauseDuringChoice) Time.timeScale = 0f;

            Signal.UI.UiModalState.Push();
            BuildOverlay(rarity, accent);
        }

        private void Choose(UpgradeOption option)
        {
            RunManager.Instance.AddUpgrade(option.ToRunUpgrade());
            Close();
        }

        private void Close()
        {
            bool wasOpen = _overlay != null;
            if (_overlay != null) Destroy(_overlay);
            _overlay = null;
            Cursor.lockState = _previousLock;
            Cursor.visible = _previousCursorVisible;
            if (pauseDuringChoice) Time.timeScale = 1f;
            if (wasOpen) Signal.UI.UiModalState.Pop();
        }

        private void OnDestroy()
        {
            if (IsOpen) Close();
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        private void BuildOverlay(ItemRarity rarity, Color accent)
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            _overlay = new GameObject("UpgradeOverlay", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = _overlay.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = _overlay.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            Image dim = NewChild<Image>(_overlay.transform, "Dim");
            dim.color = DimColor;
            Stretch(dim.rectTransform);

            Text title = NewChild<Text>(_overlay.transform, "Title");
            SetupText(title, $"{rarity} Loot".ToUpperInvariant(), 46, FontStyle.Bold, TextAnchor.MiddleCenter);
            title.color = accent;
            title.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            title.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            title.rectTransform.anchoredPosition = new Vector2(0f, -120f);
            title.rectTransform.sizeDelta = new Vector2(900f, 70f);

            Text subtitle = NewChild<Text>(_overlay.transform, "Subtitle");
            SetupText(subtitle, "Choose an upgrade", 24, FontStyle.Normal, TextAnchor.MiddleCenter);
            subtitle.color = new Color(0.85f, 0.85f, 0.9f);
            subtitle.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            subtitle.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            subtitle.rectTransform.anchoredPosition = new Vector2(0f, -175f);
            subtitle.rectTransform.sizeDelta = new Vector2(900f, 40f);

            var row = NewChild<HorizontalLayoutGroup>(_overlay.transform, "Choices");
            row.spacing = 40f;
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childControlWidth = false;
            row.childControlHeight = false;
            var rowRect = (RectTransform)row.transform;
            rowRect.anchorMin = new Vector2(0.5f, 0.5f);
            rowRect.anchorMax = new Vector2(0.5f, 0.5f);
            rowRect.sizeDelta = new Vector2(1200f, 420f);

            foreach (UpgradeOption option in _options)
                BuildCard(row.transform, option, accent);
        }

        private void BuildCard(Transform parent, UpgradeOption option, Color accent)
        {
            Image card = NewChild<Image>(parent, $"Card_{option.DisplayName}");
            card.color = CardColor;
            card.rectTransform.sizeDelta = new Vector2(300f, 380f);

            Button button = card.gameObject.AddComponent<Button>();
            button.targetGraphic = card;
            UpgradeOption captured = option;
            button.onClick.AddListener(() => Choose(captured));

            var layout = card.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(20, 20, 24, 24);
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            Image stripe = NewChild<Image>(card.transform, "RarityStripe");
            stripe.color = accent;
            stripe.gameObject.AddComponent<LayoutElement>().preferredHeight = 6f;

            if (option.Icon != null)
            {
                Image icon = NewChild<Image>(card.transform, "Icon");
                icon.sprite = option.Icon;
                icon.preserveAspect = true;
                icon.gameObject.AddComponent<LayoutElement>().preferredHeight = 96f;
            }

            Text bonus = NewChild<Text>(card.transform, "Bonus");
            SetupText(bonus, option.Label, 30, FontStyle.Bold, TextAnchor.MiddleCenter);
            bonus.gameObject.AddComponent<LayoutElement>().preferredHeight = 80f;

            Text statName = NewChild<Text>(card.transform, "Stat");
            SetupText(statName, option.DisplayName, 20, FontStyle.Normal, TextAnchor.MiddleCenter);
            statName.color = new Color(0.7f, 0.7f, 0.75f);
            statName.gameObject.AddComponent<LayoutElement>().preferredHeight = 30f;
        }

        private static T NewChild<T>(Transform parent, string name) where T : Component
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(T));
            go.transform.SetParent(parent, worldPositionStays: false);
            return go.GetComponent<T>();
        }

        private void SetupText(Text text, string content, int size, FontStyle style, TextAnchor anchor)
        {
            text.text = content;
            text.font = _font;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = anchor;
            text.color = Color.white;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
