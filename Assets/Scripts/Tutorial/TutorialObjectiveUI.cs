using System.Collections;
using System.Collections.Generic;
using Signal.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Signal.Tutorial
{
    /// <summary>
    /// On-screen checklist for the active tutorial step. It is deliberately dumb: it renders whatever
    /// <see cref="TutorialStep.Objectives"/> the step exposes and refreshes a row when that step
    /// raises <see cref="TutorialStep.ObjectiveChanged"/> — no polling, and no knowledge of what any
    /// objective means. A new step with any number of objectives needs no change here.
    ///
    /// Completed rows tick, recolor and fade but stay listed until the whole step ends.
    /// </summary>
    public class TutorialObjectiveUI : MonoBehaviour
    {
        [Header("Glyphs")]
        [SerializeField]
        [Tooltip("Shown before an incomplete objective. Change if your font lacks the ballot-box glyph.")]
        private string incompleteGlyph = "☐";
        [SerializeField]
        [Tooltip("Shown before a completed objective.")]
        private string completeGlyph = "☑";

        [Header("Style")]
        [SerializeField] private Color incompleteColor = new Color(0.94f, 0.94f, 0.97f);
        [SerializeField] private Color completeColor = new Color(0.45f, 0.82f, 0.5f, 0.65f);
        [SerializeField, Min(0f)]
        [Tooltip("Seconds of the little scale-pop played when an objective ticks. 0 = no animation.")]
        private float completePopDuration = 0.22f;
        [SerializeField, Min(1f)] private float completePopScale = 1.12f;

        private static readonly Color PanelColor = new Color(0.06f, 0.06f, 0.09f, 0.72f);

        private GameObject _panel;
        private Text _titleText;
        private RectTransform _list;

        private readonly Dictionary<TutorialObjective, Text> _rows = new Dictionary<TutorialObjective, Text>();
        private TutorialStep _step;

        private void Awake()
        {
            Build();
            _panel.SetActive(false);
        }

        private void OnDestroy() => Unsubscribe();

        /// <summary>Renders <paramref name="step"/>'s checklist and follows it until Hide/another Show.</summary>
        public void Show(TutorialStep step)
        {
            Unsubscribe();
            ClearRows();

            _step = step;
            if (_step == null) { _panel.SetActive(false); return; }

            _titleText.text = _step.Title;
            foreach (TutorialObjective objective in _step.Objectives)
                AddRow(objective);

            _step.ObjectiveChanged += OnObjectiveChanged;

            // A step with no objectives (or one that completed instantly) shows nothing.
            _panel.SetActive(_rows.Count > 0);
        }

        public void Hide()
        {
            Unsubscribe();
            ClearRows();
            if (_panel != null) _panel.SetActive(false);
        }

        private void Unsubscribe()
        {
            if (_step != null) _step.ObjectiveChanged -= OnObjectiveChanged;
            _step = null;
        }

        // The only update path — driven by the step's event, never by polling.
        private void OnObjectiveChanged(TutorialObjective objective)
        {
            if (!_rows.TryGetValue(objective, out Text row)) return;

            ApplyRow(row, objective);
            if (objective.IsComplete && completePopDuration > 0f && isActiveAndEnabled)
                StartCoroutine(Pop(row.rectTransform));
        }

        private void AddRow(TutorialObjective objective)
        {
            Text row = UiBuilder.CreateText(_list, $"Objective_{_rows.Count}", "", 22, FontStyle.Normal, TextAnchor.MiddleLeft);
            row.horizontalOverflow = HorizontalWrapMode.Wrap;
            row.gameObject.AddComponent<LayoutElement>().minHeight = 30f;
            _rows[objective] = row;
            ApplyRow(row, objective);
        }

        private void ApplyRow(Text row, TutorialObjective objective)
        {
            string box = objective.IsComplete ? completeGlyph : incompleteGlyph;
            string counter = objective.HasProgressCounter && !objective.IsComplete
                ? $"  ({objective.Progress}/{objective.Target})"
                : "";

            row.text = $"{box}  {objective.Text}{counter}";
            row.color = objective.IsComplete ? completeColor : incompleteColor;
            row.fontStyle = objective.IsComplete ? FontStyle.Italic : FontStyle.Normal;
        }

        // Small "just ticked" flourish. Unscaled so it still plays if something paused the game.
        private IEnumerator Pop(RectTransform rect)
        {
            float half = completePopDuration * 0.5f;

            for (float t = 0f; t < half; t += Time.unscaledDeltaTime)
            {
                if (rect == null) yield break;
                rect.localScale = Vector3.one * Mathf.Lerp(1f, completePopScale, t / half);
                yield return null;
            }
            for (float t = 0f; t < half; t += Time.unscaledDeltaTime)
            {
                if (rect == null) yield break;
                rect.localScale = Vector3.one * Mathf.Lerp(completePopScale, 1f, t / half);
                yield return null;
            }
            if (rect != null) rect.localScale = Vector3.one;
        }

        private void ClearRows()
        {
            foreach (KeyValuePair<TutorialObjective, Text> pair in _rows)
                if (pair.Value != null) Destroy(pair.Value.gameObject);
            _rows.Clear();
        }

        // ── UI construction ───────────────────────────────────────────────────

        private void Build()
        {
            // Below the prompt canvas (40) so a paused prompt always reads on top.
            Canvas canvas = UiBuilder.CreateOverlayCanvas("TutorialObjectiveCanvas", 30);
            _panel = canvas.gameObject;

            Image bg = UiBuilder.NewChild<Image>(canvas.transform, "ObjectivePanel");
            bg.color = PanelColor;
            bg.raycastTarget = false;

            RectTransform r = bg.rectTransform;
            r.anchorMin = new Vector2(1f, 1f);      // top-right HUD corner
            r.anchorMax = new Vector2(1f, 1f);
            r.pivot = new Vector2(1f, 1f);
            r.anchoredPosition = new Vector2(-40f, -40f);
            r.sizeDelta = new Vector2(460f, 190f);

            // Height follows the number of rows, so any objective count fits.
            var fitter = bg.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var layout = bg.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(18, 18, 14, 16);
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            _titleText = UiBuilder.CreateText(bg.transform, "Title", "", 24, FontStyle.Bold, TextAnchor.MiddleLeft);
            _titleText.gameObject.AddComponent<LayoutElement>().minHeight = 32f;

            _list = (RectTransform)bg.transform; // rows are laid out by the same vertical group
        }
    }
}
