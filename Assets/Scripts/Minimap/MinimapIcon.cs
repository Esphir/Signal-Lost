// One room type's icon on the minimap: the pairing of a RoomType to the sprite (and tint) that represents it.
using System;
using Signal.Generation;
using UnityEngine;

namespace Signal.Minimap
{
    [Serializable]
    public class MinimapIcon
    {
        [Tooltip("Room type this icon represents.")]
        public RoomType type;

        [Tooltip("Sprite drawn on the room's tile. Leave empty for no icon (e.g. a plain Transition).")]
        public Sprite sprite;

        [Tooltip("Multiplied into the icon colour, so one sprite can be re-tinted per type.")]
        public Color tint = Color.white;
    }
}
