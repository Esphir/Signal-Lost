using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Signal.UI
{
    /// <summary>
    /// Draws a bright border around whatever the EventSystem currently has selected, so a controller player
    /// can see where they are. It matters far more on a pad than with a mouse: there's no cursor to point
    /// at the answer, so the selection *is* the cursor.
    ///
    /// A border rather than a stronger tint, because a tint can't do the job here — Unity's ColorBlock
    /// multiplies the graphic's own colour and vertex colours clamp at 1, so on the dark panels this game
    /// uses a highlight can only ever come out darker than the button already is. An outline is additive
    /// and reads on any background.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Graphic))]
    public sealed class SelectionHighlight : MonoBehaviour, ISelectHandler, IDeselectHandler
    {
        private static readonly Color BorderColor = new Color(1f, 1f, 1f, 0.95f);

        [SerializeField, Min(1f)] private float thickness = 4f;

        private Outline _outline;

        /// <summary>Adds a highlight to a control, or retunes the one it already has.</summary>
        public static SelectionHighlight Attach(GameObject target, float thickness = 4f)
        {
            if (target == null || target.GetComponent<Graphic>() == null) return null;

            SelectionHighlight highlight = target.GetComponent<SelectionHighlight>();
            if (highlight == null) highlight = target.AddComponent<SelectionHighlight>();

            highlight.thickness = Mathf.Max(1f, thickness);
            highlight.Build();
            return highlight;
        }

        private void Awake() => Build();

        private void Build()
        {
            if (_outline == null)
            {
                _outline = GetComponent<Outline>();
                if (_outline == null) _outline = gameObject.AddComponent<Outline>();
            }

            _outline.effectColor = BorderColor;
            _outline.effectDistance = new Vector2(thickness, thickness);
            _outline.useGraphicAlpha = false; // the border stays solid even over a faded card
            _outline.enabled = IsSelected;
        }

        private bool IsSelected =>
            EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject;

        public void OnSelect(BaseEventData eventData) => Show(true);

        public void OnDeselect(BaseEventData eventData) => Show(false);

        private void Show(bool on)
        {
            if (_outline != null) _outline.enabled = on;
        }
    }
}
