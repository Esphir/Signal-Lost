// Decides whether a placement is legal, and audits the finished level.
using System.Collections.Generic;
using UnityEngine;

namespace Signal.Generation
{
    public class RoomValidator
    {
        private readonly float _tolerance;

        public RoomValidator(float overlapTolerance) => _tolerance = overlapTolerance;

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

        public static bool CanMate(RoomConnector source, RoomConnector target, bool allowRotation)
        {
            if (source == null || target == null) return false;
            if (source.IsOccupied || target.IsOccupied) return false;
            if (source.ConnectionType != target.ConnectionType) return false;

            bool sourceVertical = source.Direction.IsVertical();
            if (sourceVertical != target.Direction.IsVertical()) return false;

            if (sourceVertical || !allowRotation)
                return target.Direction == source.Direction.Opposite();

            return true;
        }

        public GenerationReport Audit(IReadOnlyList<RoomDefinition> placed, bool allowOpenEnds)
        {
            var report = new GenerationReport { RoomCount = placed.Count };

            for (int i = 0; i < placed.Count; i++)
            {
                RoomDefinition room = placed[i];
                if (room == null) { report.Problems.Add($"Room #{i} is null."); continue; }

                for (int j = i + 1; j < placed.Count; j++)
                {
                    if (placed[j] == null) continue;
                    if (Shrink(room.WorldBounds).Intersects(Shrink(placed[j].WorldBounds)))
                    {
                        report.Overlaps++;
                        report.Problems.Add($"Rooms #{i} ({room.name}) and #{j} ({placed[j].name}) overlap.");
                    }
                }

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
                if (room.RoomType == RoomType.End) report.EndRooms++;
                if (room.Checkpoints is { Length: > 0 }) report.CheckpointRooms++;
                if (room.SpawnSections is { Length: > 0 }) report.SpawnSectionRooms++;
            }

            if (!allowOpenEnds && report.OpenConnectors > 0)
                report.Problems.Add($"{report.OpenConnectors} connector(s) left open (unintentional dead ends).");

            report.UnreachableRooms = CountUnreachable(placed);
            if (report.UnreachableRooms > 0)
                report.Problems.Add($"{report.UnreachableRooms} room(s) unreachable from the Start room.");

            return report;
        }

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

    public class GenerationReport
    {
        public int RoomCount;
        public int OpenConnectors;
        public int SealedConnectors;
        public int UnreachableRooms;
        public int Overlaps;
        public int EndRooms;
        public int CheckpointRooms;
        public int SpawnSectionRooms;
        public readonly List<string> Problems = new List<string>();

        public bool IsValid => Problems.Count == 0;

        public override string ToString()
            => $"{RoomCount} rooms, {EndRooms} exit(s), {CheckpointRooms} with checkpoints, {SpawnSectionRooms} with spawns, " +
               $"{SealedConnectors} sealed dead ends, {UnreachableRooms} unreachable, {Problems.Count} problem(s)";
    }
}
