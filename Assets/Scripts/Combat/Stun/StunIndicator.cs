// A small "stunned" icon that floats above a target in world space and billboards toward the camera — the same world-space-canvas + LateUpdate-billboard approach used by the enemy health bars.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Signal.Combat.Stun
{
    public class StunIndicator : MonoBehaviour
    {
        private Canvas _canvas;
        private Text _label;
        private Transform _follow;
        private Vector3 _offset;
        private Camera _camera;

        public void Show(Transform follow, Vector3 offset, Camera billboardCamera, string glyph, Color color)
        {
            EnsureBuilt();
            _follow = follow;
            _offset = offset;
            _camera = billboardCamera;
            _label.text = glyph;
            _label.color = color;
            _canvas.enabled = true;
            SnapToTarget();
        }

        public void Hide() => StunIndicatorPool.Release(this);

        internal void Detach()
        {
            _follow = null;
            if (_canvas != null) _canvas.enabled = false;
        }

        private void EnsureBuilt()
        {
            if (_canvas != null) return;

            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;

            var rect = (RectTransform)transform;
            rect.sizeDelta = new Vector2(100f, 100f);
            transform.localScale = Vector3.one * 0.01f;

            _label = UiBuilderStun.CreateGlyph(transform);
        }

        private void LateUpdate()
        {
            if (_follow == null)
            {
                if (_canvas != null && _canvas.enabled) StunIndicatorPool.Release(this);
                return;
            }
            SnapToTarget();
        }

        private void SnapToTarget()
        {
            transform.position = _follow.position + _offset;

            Camera cam = _camera != null ? _camera : Camera.main;
            if (cam != null)
                transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
        }
    }

    public static class StunIndicatorPool
    {
        private static readonly Stack<StunIndicator> Pool = new Stack<StunIndicator>();

        public static StunIndicator Get()
        {
            StunIndicator indicator = null;
            while (Pool.Count > 0 && indicator == null) indicator = Pool.Pop();

            if (indicator == null)
            {
                var go = new GameObject("StunIndicator", typeof(RectTransform));
                indicator = go.AddComponent<StunIndicator>();
            }
            else
            {
                indicator.gameObject.SetActive(true);
            }
            return indicator;
        }

        public static void Release(StunIndicator indicator)
        {
            if (indicator == null) return;
            indicator.Detach();
            indicator.gameObject.SetActive(false);
            Pool.Push(indicator);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Pool.Clear();
    }

    internal static class UiBuilderStun
    {
        public static Text CreateGlyph(Transform parent)
        {
            var go = new GameObject("Glyph", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, worldPositionStays: false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 80;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }
    }
}
