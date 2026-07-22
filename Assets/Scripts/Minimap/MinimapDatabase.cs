// Every sprite the minimap draws, as data.
using System.Collections.Generic;
using Signal.Generation;
using UnityEngine;

namespace Signal.Minimap
{
    [CreateAssetMenu(menuName = "Signal Lost/Minimap/Minimap Database", fileName = "MinimapDatabase")]
    public class MinimapDatabase : ScriptableObject
    {
        [Header("Tile Background (by fog state)")]
        [Tooltip("Undiscovered rooms adjacent to explored ones. Fully-hidden rooms aren't drawn at all.")]
        public Sprite unknownRoom;
        [Tooltip("Discovered but not yet entered — shown dimmed.")]
        public Sprite discoveredRoom;
        [Tooltip("Entered earlier — shown at normal brightness.")]
        public Sprite visitedRoom;
        [Tooltip("The room the player is in right now.")]
        public Sprite currentRoom;

        [Header("Tile Overlays")]
        [Tooltip("Frame drawn on every visible tile. Optional.")]
        public Sprite border;
        [Tooltip("Extra highlight drawn only on the current room (glow/ring). Optional.")]
        public Sprite currentIndicator;

        [Header("Connections")]
        [Tooltip("Sprite for the line between two connected rooms. A plain white sprite works.")]
        public Sprite connection;

        [Header("Room Type Icons")]
        [SerializeField]
        [Tooltip("One entry per room type. Types without an entry simply draw no icon.")]
        private List<MinimapIcon> icons = new List<MinimapIcon>();

        private Dictionary<RoomType, MinimapIcon> _byType;

        public MinimapIcon GetIcon(RoomType type)
        {
            if (_byType == null) BuildLookup();
            return _byType.TryGetValue(type, out MinimapIcon icon) ? icon : null;
        }

        public Sprite Background(bool isCurrent, bool isVisited, bool isDiscovered)
        {
            if (isCurrent) return currentRoom != null ? currentRoom : visitedRoom;
            if (isVisited) return visitedRoom;
            if (isDiscovered) return discoveredRoom != null ? discoveredRoom : unknownRoom;
            return unknownRoom;
        }

        private void BuildLookup()
        {
            _byType = new Dictionary<RoomType, MinimapIcon>();
            foreach (MinimapIcon icon in icons)
                if (icon != null) _byType[icon.type] = icon;
        }

        private void OnValidate() => _byType = null;
    }
}
