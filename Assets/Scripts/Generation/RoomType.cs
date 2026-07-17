namespace Signal.Generation
{
    /// <summary>
    /// What role a room plays in a run. Drives selection rules (checkpoint cadence, combat streaks)
    /// rather than anything visual — a room's looks are entirely the prefab's business.
    /// </summary>
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

    /// <summary>
    /// Which way a connector faces. Up/Down exist now so vertical rooms need no enum change later;
    /// the generator matches a connector to its opposite, so vertical linking already works.
    /// </summary>
    public enum ConnectorDirection
    {
        North,
        South,
        East,
        West,
        Up,
        Down,
    }

    /// <summary>
    /// The "shape" of an opening. Only matching types may mate, so a boss gate or a crawl-vent can
    /// never join a standard corridor. Add values freely — nothing switches on this.
    /// </summary>
    public enum ConnectionType
    {
        Standard,
        Large,
        Vent,
        Boss,
    }

    /// <summary>How an unused doorway is closed off so it never exposes the void.</summary>
    public enum DeadEndTreatment
    {
        /// <summary>Leave the room's own blocking wall in place. Cheapest, and always correct.</summary>
        Wall,
        ClosedDoor,
        Rubble,
        CaveIn,
    }

    public static class ConnectorDirectionExtensions
    {
        /// <summary>The direction a connector must face to mate with this one.</summary>
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
    }
}
