// Draws a bright border around whatever the EventSystem currently has selected, so a controller player can see where they are.
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Signal.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Graphic))]
    public sealed class SelectionHighlight : MonoBehaviour, ISelectHandler, IDeselectHandler
    {
        private static readonly Color BorderColor = new Color(1f, 1f, 1f, 0.95f);

        [SerializeField, Min(1f)] private float thickness = 4f;

        private Outline _outline;

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
            _outline.useGraphicAlpha = false;
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
