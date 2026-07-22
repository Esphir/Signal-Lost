// Makes sure a controller always has something to navigate from.
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Signal.UI
{
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

            GameObject restore = _lastValid != null && _lastValid.activeInHierarchy && Interactable(_lastValid)
                ? _lastValid
                : FirstSelectable();

            if (restore == null) return;
            events.SetSelectedGameObject(restore);
            _lastValid = restore;
        }

        private static bool Interactable(GameObject go)
            => go.TryGetComponent(out Selectable selectable) && Eligible(selectable);

        private static bool Eligible(Selectable selectable)
            => selectable != null && selectable.isActiveAndEnabled && selectable.IsInteractable() &&
               selectable.navigation.mode != Navigation.Mode.None;

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
