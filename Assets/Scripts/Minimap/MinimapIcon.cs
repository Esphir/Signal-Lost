using System;
using Signal.Generation;
using UnityEngine;

namespace Signal.Minimap
{
    /// <summary>
    /// One room type's icon on the minimap: the pairing of a <see cref="RoomType"/> to the sprite (and
    /// tint) that represents it. Pure data — adding a Boss, Shop or Secret icon later is one more entry
    /// in the database, no code. Kept a separate type so the icon set stays open for extension.
    /// </summary>
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
