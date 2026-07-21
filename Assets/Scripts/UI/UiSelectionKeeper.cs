using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Signal.UI
{
    /// <summary>
    /// Makes sure a controller always has something to navigate from. Gamepad menu input is relative to the
    /// current selection, so the instant nothing is selected the whole menu goes dead — and that happens
    /// constantly: clicking empty space clears the selection, and a screen that never set one has no
    /// selection to begin with. This watches for that and re-selects, so picking up a controller in front
    /// of any menu in the game just works.
    ///
    /// It only acts while a controller is in use. Restoring selection for a mouse player would paint a
    /// highlight on a button they never chose and make the menu look stuck.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UiSelectionKeeper : MonoBehaviour
    {
        private GameObject _lastValid;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var go = new GameObject("UiSelectionKeeper");
            DontDestroyOnLoad(go);
            go.AddComponent<UiSelectionKeeper>();
        }

        private void Update()
        {
            EventSystem events = EventSystem.current;
            if (events == null) return;

            GameObject selected = events.currentSelectedGameObject;
            if (selected != null && selected.activeInHierarchy)
            {
                _lastValid = selected;
                return;
            }

            if (!InputSchemeTracker.UsingGamepad) return;

            // Prefer whatever the player was last on — returning focus where they left it beats jumping to
            // the top of the menu after an accidental click.
            GameObject restore = _lastValid != null && _lastValid.activeInHierarchy && Interactable(_lastValid)
                ? _lastValid
                : FirstSelectable();

            if (restore == null) return;
            events.SetSelectedGameObject(restore);
            _lastValid = restore;
        }

        private static bool Interactable(GameObject go)
            => go.TryGetComponent(out Selectable selectable) && Eligible(selectable);

        /// <summary>
        /// A control a controller may be given. Navigation set to None means "mouse only" — the tutorial
        /// prompt's Continue button uses it so that focusing nothing is a stable state there rather than
        /// something this keeper immediately undoes.
        /// </summary>
        private static bool Eligible(Selectable selectable)
            => selectable != null && selectable.isActiveAndEnabled && selectable.IsInteractable() &&
               selectable.navigation.mode != Navigation.Mode.None;

        /// <summary>
        /// The best control to fall back to: the first eligible one on the front-most canvas that has any
        /// controls at all. Focus never reaches behind that canvas — a modal in front must either take
        /// focus or hold it empty, never hand it to a button buried underneath.
        /// </summary>
        private static GameObject FirstSelectable()
        {
            int frontOrder = int.MinValue;
            foreach (Selectable selectable in Selectable.allSelectablesArray)
            {
                if (selectable == null || !selectable.isActiveAndEnabled) continue;
                frontOrder = Mathf.Max(frontOrder, SortingOrder(selectable));
            }
            if (frontOrder == int.MinValue) return null;

            foreach (Selectable selectable in Selectable.allSelectablesArray)
                if (Eligible(selectable) && SortingOrder(selectable) == frontOrder)
                    return selectable.gameObject;

            return null;
        }

        private static int SortingOrder(Selectable selectable)
        {
            Canvas canvas = selectable.GetComponentInParent<Canvas>();
            return canvas != null && canvas.rootCanvas != null ? canvas.rootCanvas.sortingOrder : 0;
        }
    }
}
