// One room's tile in the minimap.
using UnityEngine;
using UnityEngine.UI;

namespace Signal.Minimap
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class MinimapRoomUI : MonoBehaviour
    {
        private RectTransform _rect;
        private MinimapDatabase _db;
        private Image _background, _border, _icon, _indicator;
        private MinimapIcon _iconDef;

        private float _reveal;
        private float _targetReveal;
        private float _scale = 1f;
        private float _targetScale = 1f;
        private float _opacity = 1f;
        private float _brightness = 1f;
        private bool _pulse;

        public void Build(MinimapDatabase db, float tileSize, float iconSize)
        {
            _db = db;
            _rect = GetComponent<RectTransform>();
            _rect.sizeDelta = new Vector2(tileSize, tileSize);

            _background = MakeImage("Background", tileSize);
            _indicator = MakeImage("Indicator", tileSize * 1.25f);
            _icon = MakeImage("Icon", iconSize);
            _border = MakeImage("Border", tileSize);

            if (_db.border != null) _border.sprite = _db.border;
            if (_db.currentIndicator != null) _indicator.sprite = _db.currentIndicator;
        }

        private Image MakeImage(string childName, float size)
        {
            var go = new GameObject(childName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(_rect, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            return img;
        }

        public void SetState(MinimapRoom room, float opacity, bool animate, bool pulse, bool revealAll)
        {
            _opacity = opacity;
            _pulse = pulse && room.IsCurrentRoom;

            if (!room.IsVisible && !revealAll)
            {
                _targetReveal = 0f;
                if (!animate) { _reveal = 0f; gameObject.SetActive(false); }
                return;
            }

            gameObject.SetActive(true);
            _targetReveal = 1f;

            _brightness = room.IsVisited || room.IsCurrentRoom ? 1f : 0.45f;
            _targetScale = room.IsCurrentRoom ? 1.12f : 1f;

            _background.sprite = _db.Background(room.IsCurrentRoom, room.IsVisited, room.IsDiscovered || revealAll);

            _iconDef = _db.GetIcon(room.RoomType);
            bool hasIcon = _iconDef != null && _iconDef.sprite != null;
            _icon.enabled = hasIcon;
            if (hasIcon) _icon.sprite = _iconDef.sprite;

            _border.enabled = _db.border != null;
            _indicator.enabled = room.IsCurrentRoom && _db.currentIndicator != null;

            if (!animate) { _reveal = 1f; _scale = _targetScale; }
            ApplyColours();
            ApplyTransform();
        }

        private void Update()
        {
            float step = Time.unscaledDeltaTime;
            _reveal = Mathf.MoveTowards(_reveal, _targetReveal, step * 4f);

            float scaleGoal = _targetScale;
            if (_pulse) scaleGoal += 0.06f * Mathf.Sin(Time.unscaledTime * 5f);
            _scale = Mathf.Lerp(_scale, scaleGoal, 1f - Mathf.Exp(-step * 10f));

            if (_targetReveal == 0f && _reveal <= 0.001f)
            {
                if (gameObject.activeSelf) gameObject.SetActive(false);
                return;
            }

            ApplyColours();
            ApplyTransform();
        }

        private void ApplyColours()
        {
            float a = _opacity * _reveal;
            _background.color = new Color(_brightness, _brightness, _brightness, a);
            _border.color = new Color(1f, 1f, 1f, a * (_targetScale > 1f ? 1f : 0.5f));
            _indicator.color = new Color(1f, 1f, 1f, a);
            if (_iconDef != null)
                _icon.color = new Color(_iconDef.tint.r, _iconDef.tint.g, _iconDef.tint.b,
                                        _iconDef.tint.a * _brightness * a);
        }

        private void ApplyTransform() => _rect.localScale = Vector3.one * _scale;
    }
}
