using System.Collections.Generic;
using UnityEngine;

namespace Signal.Generation
{
    /// <summary>
    /// Decides whether a placement is legal, and audits the finished level. Pure checks — it places
    /// nothing and chooses nothing (Single Responsibility), so the overlap rule can be reasoned about
    /// on its own.
    /// </summary>
    public class RoomValidator
    {
        private readonly float _tolerance;

        public RoomValidator(float overlapTolerance) => _tolerance = overlapTolerance;

        /// <summary>
        /// True when a room at its current pose clears every already-placed room. Bounds are shrunk by
        /// the tolerance so neighbours may share a wall at a doorway without reading as an overlap.
        /// </summary>
        public bool IsClear(RoomDefinition candidate, IReadOnlyList<RoomDefinition> placed, RoomDefinition ignore = null)
        {
            Bounds a = Shrink(candidate.WorldBounds);

            foreach (RoomDefinition other in placed)
            {
                if (other == null || other == candidate || other == ignore) continue;
                if (a.Intersects(Shrink(other.WorldBounds))) return false;
            }
            return true;
        }

        /// <summary>
        /// Whether two connectors may be joined.
        ///
        /// Both must be free and of the same <see cref="ConnectionType"/>. Direction is where rotation
        /// changes the answer: without it, the candidate's authored direction must already be the
        /// opposite of the opening's (North↔South, East↔West). With rotation allowed, any horizontal
        /// pair can mate, because the generator simply turns the room until it does — that is what
        /// lets an East door become a North door.
        ///
        /// Vertical connectors never mate with horizontal ones: rooms rotate about Y only, so no
        /// amount of turning points a floor hatch at a wall.
        /// </summary>
        public static bool CanMate(RoomConnector source, RoomConnector target, bool allowRotation)
        {
            if (source == null || target == null) return false;
            if (source.IsOccupied || target.IsOccupied) return false;
            if (source.ConnectionType != target.ConnectionType) return false;

            bool sourceVertical = source.Direction.IsVertical();
            if (sourceVertical != target.Direction.IsVertical()) return false;

            // Vertical pairs must genuinely oppose: yaw can't turn Up into Down.
            if (sourceVertical || !allowRotation)
                return target.Direction == source.Direction.Opposite();

            return true;
        }

        /// <summary>
        /// Post-generation audit. Reports problems rather than throwing, so a bad database yields a
        /// diagnosable level instead of a silent failure.
        /// </summary>
        public GenerationReport Audit(IReadOnlyList<RoomDefinition> placed, bool allowOpenEnds)
        {
            var report = new GenerationReport { RoomCount = placed.Count };

            for (int i = 0; i < placed.Count; i++)
            {
                RoomDefinition room = placed[i];
                if (room == null) { report.Problems.Add($"Room #{i} is null."); continue; }

                // Overlap: every pair, against the same rule used during placement.
                for (int j = i + 1; j < placed.Count; j++)
                {
                    if (placed[j] == null) continue;
                    if (Shrink(room.WorldBounds).Intersects(Shrink(placed[j].WorldBounds)))
                        report.Problems.Add($"Rooms #{i} ({room.name}) and #{j} ({placed[j].name}) overlap.");
                }

                // Connectivity: every room past the start must be mated to something.
                bool connected = false;
                int open = 0;
                foreach (RoomConnector connector in room.Connectors)
                {
                    if (connector == null) { report.Problems.Add($"Room #{i} ({room.name}) has a null connector."); continue; }
                    if (connector.IsOccupied) connected = true;
                    else open++;
                }

                if (i > 0 && !connected)
                    report.Problems.Add($"Room #{i} ({room.name}) is disconnected.");
                if (room.Connectors.Count == 0)
                    report.Problems.Add($"Room #{i} ({room.name}) has no connectors at all.");

                if (open > 0) report.OpenConnectors += open;
                if (room.Checkpoints is { Length: > 0 }) report.CheckpointRooms++;
                if (room.SpawnSections is { Length: > 0 }) report.SpawnSectionRooms++;
            }

            // An unsealed doorway is a dead end. Fine for a corridor stub, not for a shipped level.
            if (!allowOpenEnds && report.OpenConnectors > 0)
                report.Problems.Add($"{report.OpenConnectors} connector(s) left open (unintentional dead ends).");

            report.UnreachableRooms = CountUnreachable(placed);
            if (report.UnreachableRooms > 0)
                report.Problems.Add($"{report.UnreachableRooms} room(s) unreachable from the Start room.");

            return report;
        }

        /// <summary>
        /// Flood-fills the connector graph from the Start room. "Has a connection" isn't the same as
        /// "reachable" — a branch could in principle close into an island — so this walks the actual
        /// edges rather than counting them.
        /// </summary>
        private static int CountUnreachable(IReadOnlyList<RoomDefinition> placed)
        {
            if (placed.Count == 0) return 0;

            var seen = new HashSet<RoomDefinition>();
            var queue = new Queue<RoomDefinition>();
            queue.Enqueue(placed[0]);
            seen.Add(placed[0]);

            while (queue.Count > 0)
            {
                RoomDefinition room = queue.Dequeue();
                foreach (RoomConnector connector in room.Connectors)
                {
                    if (connector == null || !connector.IsOccupied) continue;

                    RoomDefinition neighbour = connector.ConnectedTo.Owner;
                    if (neighbour == null || !seen.Add(neighbour)) continue;
                    queue.Enqueue(neighbour);
                }
            }

            int unreachable = 0;
            foreach (RoomDefinition room in placed)
                if (room != null && !seen.Contains(room)) unreachable++;
            return unreachable;
        }

        private Bounds Shrink(Bounds bounds)
        {
            Vector3 size = bounds.size - Vector3.one * (_tolerance * 2f);
            size = Vector3.Max(size, Vector3.one * 0.01f);
            return new Bounds(bounds.center, size);
        }
    }

    /// <summary>Outcome of an audit — surfaced in the log and the generator's Inspector.</summary>
    public class GenerationReport
    {
        public int RoomCount;
        public int OpenConnectors;
        public int SealedConnectors;
        public int UnreachableRooms;
        public int CheckpointRooms;
        public int SpawnSectionRooms;
        public readonly List<string> Problems = new List<string>();

        public bool IsValid => Problems.Count == 0;

        public override string ToString()
            => $"{RoomCount} rooms, {CheckpointRooms} with checkpoints, {SpawnSectionRooms} with spawns, " +
               $"{SealedConnectors} sealed dead ends, {UnreachableRooms} unreachable, {Problems.Count} problem(s)";
    }
}
