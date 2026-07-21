using System.Collections.Generic;
using Signal.Generation;
using UnityEngine;

namespace Signal.Minimap
{
    /// <summary>
    /// The minimap's model of one room: where it sits on the grid, what type it is, which directions it
    /// connects and to whom, and its exploration state. It knows nothing about UI or world geometry — it
    /// is the data the <see cref="MinimapManager"/> reasons over and the <see cref="MinimapRoomUI"/>
    /// renders, which keeps state (here) cleanly apart from presentation (there).
    /// </summary>
    public class MinimapRoom
    {
        public RoomDefinition Source { get; }

        /// <summary>Cell on the minimap grid. Seeded from the room's own cell, then re-packed by the
        /// manager so hidden hallways collapse and the real rooms they bridge sit adjacent.</summary>
        public Vector2Int GridPosition { get; set; }
        public RoomType RoomType { get; }

        /// <summary>Directions with a real connection, for drawing edges. No diagonals — always N/S/E/W.</summary>
        public HashSet<ConnectorDirection> Connections { get; } = new HashSet<ConnectorDirection>();

        /// <summary>The rooms this one is actually joined to — the set discovered when this room is entered.</summary>
        public List<MinimapRoom> Neighbours { get; } = new List<MinimapRoom>();

        public bool IsDiscovered { get; set; }
        public bool IsVisited { get; set; }
        public bool IsCurrentRoom { get; set; }

        /// <summary>The tile rendering this room, or null before the UI is built.</summary>
        public MinimapRoomUI View { get; set; }

        public MinimapRoom(RoomDefinition source)
        {
            Source = source;
            GridPosition = source.GridPosition;
            RoomType = source.RoomType;
        }

        /// <summary>True when the tile should be drawn at all — hidden rooms are simply absent.</summary>
        public bool IsVisible => IsCurrentRoom || IsVisited || IsDiscovered;
    }
}
