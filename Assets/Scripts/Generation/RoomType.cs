// What role a room plays in a run.
using UnityEngine;

namespace Signal.Generation
{
    public enum RoomType
    {
        Start,
        Combat,
        Platforming,
        Treasure,
        Checkpoint,
        Transition,
        Boss,
        End,
    }

    public enum ConnectorDirection
    {
        North,
        South,
        East,
        West,
        Up,
        Down,
    }

    public enum ConnectionType
    {
        Standard,
        Large,
        Vent,
        Boss,
    }

    public enum DeadEndTreatment
    {
        Wall,
        ClosedDoor,
        Rubble,
        CaveIn,
    }

    public static class ConnectorDirectionExtensions
    {
        public static ConnectorDirection Opposite(this ConnectorDirection direction) => direction switch
        {
            ConnectorDirection.North => ConnectorDirection.South,
            ConnectorDirection.South => ConnectorDirection.North,
            ConnectorDirection.East => ConnectorDirection.West,
            ConnectorDirection.West => ConnectorDirection.East,
            ConnectorDirection.Up => ConnectorDirection.Down,
            _ => ConnectorDirection.Up,
        };

        public static bool IsVertical(this ConnectorDirection direction)
            => direction is ConnectorDirection.Up or ConnectorDirection.Down;

        public static Vector2Int ToGridOffset(this ConnectorDirection direction) => direction switch
        {
            ConnectorDirection.North => new Vector2Int(0, 1),
            ConnectorDirection.South => new Vector2Int(0, -1),
            ConnectorDirection.East => new Vector2Int(1, 0),
            ConnectorDirection.West => new Vector2Int(-1, 0),
            _ => Vector2Int.zero,
        };
    }
}
