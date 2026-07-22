// The minimap's model of one room: where it sits on the grid, what type it is, which directions it connects and to whom, and its exploration state.
using System.Collections.Generic;
using Signal.Generation;
using UnityEngine;

namespace Signal.Minimap
{
    public class MinimapRoom
    {
        public RoomDefinition Source { get; }

        public Vector2Int GridPosition { get; set; }
        public RoomType RoomType { get; }

        public HashSet<ConnectorDirection> Connections { get; } = new HashSet<ConnectorDirection>();

        public List<MinimapRoom> Neighbours { get; } = new List<MinimapRoom>();

        public bool IsDiscovered { get; set; }
        public bool IsVisited { get; set; }
        public bool IsCurrentRoom { get; set; }

        public MinimapRoomUI View { get; set; }

        public MinimapRoom(RoomDefinition source)
        {
            Source = source;
            GridPosition = source.GridPosition;
            RoomType = source.RoomType;
        }

        public bool IsVisible => IsCurrentRoom || IsVisited || IsDiscovered;
    }
}
