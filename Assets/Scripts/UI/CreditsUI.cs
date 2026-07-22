// The credits screen: everything in the game that someone else made.
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Signal.UI
{
    public sealed class CreditsUI : MonoBehaviour
    {
        private readonly struct Section
        {
            public readonly string Heading;
            public readonly string[] Items;
            public Section(string heading, params string[] items) { Heading = heading; Items = items; }
        }

        private static readonly Section[] Sections =
        {
            new Section("Art & Animation",
                "Kevin Iglesias — Human Melee Animations 2.0 (FREE)",
                "Kevin Iglesias — Human Character Dummy",
                "Danvil — Kit01: Sword and Shield"),

            new Section("Engine & Packages — Unity Technologies",
                "Unity Engine",
                "Universal Render Pipeline",
                "Cinemachine",
                "Input System",
                "ProBuilder",
                "AI Navigation",
                "Timeline",
                "Visual Scripting",
                "uGUI"),

            new Section("Everything Else",
                "Design, code, levels and remaining art by Jared Fisher"),
        };

        private static readonly Color DimColor = new Color(0.05f, 0.05f, 0.08f, 0.97f);
        private static readonly Color ButtonColor = new Color(0.16f, 0.16f, 0.22f);
        private static readonly Color HeadingColor = new Color(0.55f, 0.8f, 1f);
        private static readonly Color ItemColor = new Color(0.85f, 0.85f, 0.9f);

        private static CreditsUI _open;

        private GameObject _restoreSelection;

        public static void Show()
        {
            if (_open != null) return;
            _open = new GameObject("CreditsScreen").AddComponent<CreditsUI>();
            _open.Build();
        }

        private void OnDestroy()
        {
            if (_open == this) _open = null;
        }

        private void Build()
        {
            UiBuilder.EnsureEventSystem();
            _restoreSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;

            Canvas canvas = UiBuilder.CreateOverlayCanvas("CreditsCanvas", 30);
            canvas.transform.SetParent(transform, false);

            Image dim = UiBuilder.NewChild<Image>(canvas.transform, "Dim");
            dim.color = DimColor;
            UiBuilder.Stretch(dim.rectTransform);

            Text title = UiBuilder.CreateText(canvas.transform, "Title", "CREDITS", 56, FontStyle.Bold, TextAnchor.MiddleCenter);
            title.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            title.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            title.rectTransform.pivot = new Vector2(0.5f, 1f);
            title.rectTransform.anchoredPosition = new Vector2(0f, -70f);
            title.rectTransform.sizeDelta = new Vector2(1000f, 90f);

            ScrollRect scroll = UiBuilder.CreateScrollView(canvas.transform, "List", out RectTransform content);
            var scrollRect = (RectTransform)scroll.transform;
            scrollRect.anchorMin = new Vector2(0.5f, 0f);
            scrollRect.anchorMax = new Vector2(0.5f, 1f);
            scrollRect.pivot = new Vector2(0.5f, 0.5f);
            scrollRect.offsetMin = new Vector2(-450f, 140f);
            scrollRect.offsetMax = new Vector2(450f, -180f);

            foreach (Section section in Sections) AddSection(content, section);

            Button back = UiBuilder.CreateButton(canvas.transform, "BackButton", "Back", ButtonColor, 26, out _);
            var backRect = (RectTransform)back.transform;
            backRect.anchorMin = new Vector2(0.5f, 0f);
            backRect.anchorMax = new Vector2(0.5f, 0f);
            backRect.pivot = new Vector2(0.5f, 0f);
            backRect.anchoredPosition = new Vector2(0f, 50f);
            backRect.sizeDelta = new Vector2(300f, 60f);
            back.onClick.AddListener(Close);

            EventSystem.current?.SetSelectedGameObject(back.gameObject);
        }

        private void AddSection(RectTransform content, Section section)
        {
            Text heading = UiBuilder.CreateText(content, $"Heading_{section.Heading}", section.Heading,
                                                28, FontStyle.Bold, TextAnchor.MiddleLeft);
            heading.color = HeadingColor;
            heading.gameObject.AddComponent<LayoutElement>().preferredHeight = 52f;

            foreach (string item in section.Items)
            {
                Text line = UiBuilder.CreateText(content, "Item", item, 21, FontStyle.Normal, TextAnchor.MiddleLeft);
                line.color = ItemColor;
                line.horizontalOverflow = HorizontalWrapMode.Wrap;
                line.gameObject.AddComponent<LayoutElement>().preferredHeight = 30f;
            }

            UiBuilder.NewRect(content, "Spacer").gameObject.AddComponent<LayoutElement>().preferredHeight = 22f;
        }

        private void Close()
        {
            if (_restoreSelection != null && _restoreSelection.activeInHierarchy)
                EventSystem.current?.SetSelectedGameObject(_restoreSelection);

            _open = null;
            Destroy(gameObject);
        }
    }
}
