// Reusable full-screen fade to/from black.
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Signal.UI
{
    public sealed class ScreenFadeController : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float fadeOutDuration = 0.35f;
        [SerializeField, Min(0f)] private float fadeInDuration = 0.5f;
        [SerializeField, Min(0f)] private float holdBlackDuration = 0.1f;
        [SerializeField]
        [Tooltip("Optional pre-built fade CanvasGroup. Empty = a black overlay canvas is created at runtime.")]
        private CanvasGroup fadeCanvas;

        public static ScreenFadeController Instance { get; private set; }

        public float FadeOutDuration => fadeOutDuration;
        public float FadeInDuration => fadeInDuration;
        public float HoldBlackDuration => holdBlackDuration;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (fadeCanvas == null) fadeCanvas = BuildCanvas();
            SetAlpha(0f);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public IEnumerator FadeOut() => FadeTo(1f, fadeOutDuration);
        public IEnumerator FadeIn() => FadeTo(0f, fadeInDuration);

        public IEnumerator HoldBlack()
        {
            if (holdBlackDuration > 0f) yield return new WaitForSecondsRealtime(holdBlackDuration);
        }

        public void SetBlack(bool black) => SetAlpha(black ? 1f : 0f);

        private IEnumerator FadeTo(float target, float duration)
        {
            if (fadeCanvas == null) yield break;
            if (duration <= 0f) { SetAlpha(target); yield break; }

            float start = fadeCanvas.alpha;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                SetAlpha(Mathf.SmoothStep(start, target, Mathf.Clamp01(t / duration)));
                yield return null;
            }
            SetAlpha(target);
        }

        private void SetAlpha(float alpha)
        {
            if (fadeCanvas == null) return;
            fadeCanvas.alpha = alpha;
            fadeCanvas.blocksRaycasts = alpha > 0.001f;
        }

        private CanvasGroup BuildCanvas()
        {
            var go = new GameObject("ScreenFadeCanvas", typeof(Canvas), typeof(CanvasGroup));
            go.transform.SetParent(transform, false);

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;

            var group = go.GetComponent<CanvasGroup>();
            group.interactable = false;

            var imageGo = new GameObject("Black", typeof(RectTransform), typeof(Image));
            imageGo.transform.SetParent(go.transform, false);
            var image = imageGo.GetComponent<Image>();
            image.color = Color.black;
            RectTransform rect = image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            return group;
        }
    }
}
