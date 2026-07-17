using UnityEngine;
using UnityEngine.EventSystems;

namespace Signal.Audio
{
    /// <summary>
    /// Turns pointer/selection events on one button into UI audio. Attached automatically by
    /// <see cref="Signal.UI.UiBuilder"/> to every button it creates, so menus get audio feedback
    /// without a single menu script referencing a sound.
    ///
    /// It resolves the controller lazily and no-ops when there isn't one, so UI still works in scenes
    /// with no audio at all (menus, tests).
    /// </summary>
    [DisallowMultipleComponent]
    public class ButtonAudioHooks : MonoBehaviour,
        IPointerEnterHandler, IPointerClickHandler, ISelectHandler, ISubmitHandler
    {
        private static IUIAudio Audio => UIAudioController.Instance;

        public void OnPointerEnter(PointerEventData eventData) => Audio?.PlayHover();
        public void OnPointerClick(PointerEventData eventData) => Audio?.PlayClick();

        /// <summary>Controller/keyboard focus lands on this button — same feedback as a mouse hover.</summary>
        public void OnSelect(BaseEventData eventData) => Audio?.PlayHover();

        /// <summary>Controller/keyboard "submit" — the gamepad equivalent of a click.</summary>
        public void OnSubmit(BaseEventData eventData) => Audio?.PlayClick();
    }
}
