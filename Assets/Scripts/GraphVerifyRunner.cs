using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Signal.Generation;
using UnityEngine;

/// <summary>
/// TEMPORARY play-mode harness: proves the generator produces a graph, not a line — and that no
/// doorway is left open. Deleted once it has run.
/// </summary>
public class GraphVerifyRunner : MonoBehaviour
{
    private IEnumerator Start()
    {
        var generator = FindFirstObjectByType<LevelGenerator>();
        if (generator == null) { Debug.LogError("[Graph] No LevelGenerator!"); Quit(); yield break; }

        // ── Linearity: does the level actually turn and branch? ──────────────
        // A straight line occupies one axis only, and every room has exactly 2 used doors.
        int straightRuns = 0, branchedRuns = 0, rotatedRuns = 0;
        int totalOpenDoors = 0, totalUnreachable = 0, totalOverlaps = 0;
        const int runs = 20;

        for (int i = 0; i < runs; i++)
        {
            SetSeed(generator, 500 + i);
            generator.Generate();
            yield return null;

            Bounds extent = LevelExtent(generator);
            bool oneAxis = extent.size.x < 25f || extent.size.z < 25f; // never left the spine
            if (oneAxis) straightRuns++;

            if (CountBranchRooms(generator) > 0) branchedRuns++;
            if (CountRotatedRooms(generator) > 0) rotatedRuns++;

            totalOpenDoors += CountOpenDoors(generator);
            totalUnreachable += generator.LastReport.UnreachableRooms;
            foreach (string p in generator.LastReport.Problems)
                if (p.Contains("overlap")) totalOverlaps++;
        }

        Debug.Log($"[Graph] {runs} seeds:");
        Debug.Log($"[Graph]   Runs confined to a single axis (straight line): {straightRuns} {(straightRuns == 0 ? "PASS" : "FAIL")}");
        Debug.Log($"[Graph]   Runs containing a branch (3+ door room): {branchedRuns} {(branchedRuns > 0 ? "PASS" : "FAIL")}");
        Debug.Log($"[Graph]   Runs containing a rotated room: {rotatedRuns} {(rotatedRuns > 0 ? "PASS" : "FAIL")}");
        Debug.Log($"[Graph]   === OPEN (unsealed) DOORWAYS: {totalOpenDoors} (expect 0) {(totalOpenDoors == 0 ? "PASS" : "FAIL")} ===");
        Debug.Log($"[Graph]   === UNREACHABLE ROOMS: {totalUnreachable} (expect 0) {(totalUnreachable == 0 ? "PASS" : "FAIL")} ===");
        Debug.Log($"[Graph]   === OVERLAPS: {totalOverlaps} (expect 0) {(totalOverlaps == 0 ? "PASS" : "FAIL")} ===");

        if (straightRuns > 0) Debug.LogError("[Graph] Still generating straight lines!");
        if (totalOpenDoors > 0) Debug.LogError("[Graph] Doorways opening into the void!");
        if (totalUnreachable > 0) Debug.LogError("[Graph] Unreachable rooms!");

        // ── Branch Chance actually changes the shape ─────────────────────────
        SetBranchChance(generator, 0f);
        SetSeed(generator, 777);
        generator.Generate();
        yield return null;
        int linearBranches = CountBranchRooms(generator);
        Bounds linearExtent = LevelExtent(generator);

        SetBranchChance(generator, 80f);
        SetSeed(generator, 777);
        generator.Generate();
        yield return null;
        int sprawlBranches = CountBranchRooms(generator);
        Bounds sprawlExtent = LevelExtent(generator);

        Debug.Log($"[Graph] Branch 0%:  {linearBranches} branch rooms, extent {linearExtent.size.x:F0}x{linearExtent.size.z:F0}");
        Debug.Log($"[Graph] Branch 80%: {sprawlBranches} branch rooms, extent {sprawlExtent.size.x:F0}x{sprawlExtent.size.z:F0}");
        Debug.Log($"[Graph] Branch Chance changes layout: {(sprawlBranches != linearBranches || sprawlExtent.size != linearExtent.size ? "PASS" : "FAIL")}");

        // ── Determinism survives the refactor ────────────────────────────────
        SetBranchChance(generator, 25f);
        SetSeed(generator, 4242);
        generator.Generate();
        string a = Fingerprint(generator);
        generator.Generate();
        string b = Fingerprint(generator);
        Debug.Log($"[Graph] Same seed still reproduces: {(a == b ? "PASS" : "FAIL")}");
        if (a != b) Debug.LogError("[Graph] Determinism broken by the refactor!");

        // ── Connectors align exactly (not room centres) ──────────────────────
        float worstGap = 0f;
        foreach (RoomDefinition room in generator.Rooms)
        {
            if (room == null) continue;
            foreach (RoomConnector c in room.Connectors)
            {
                if (c == null || !c.IsOccupied) continue;
                worstGap = Mathf.Max(worstGap, Vector3.Distance(c.transform.position, c.ConnectedTo.transform.position));
            }
        }
        Debug.Log($"[Graph] Worst connector misalignment: {worstGap:F4}m {(worstGap < 0.01f ? "PASS — doorways coincide exactly" : "FAIL")}");
        if (worstGap >= 0.01f) Debug.LogError("[Graph] Connectors not aligning!");

        // ── Existing systems still work inside the rebuilt rooms ─────────────
        GameObject player = GameObject.FindWithTag("Player");
        Vector3 playerPos = player != null ? player.transform.position : Vector3.zero;

        int enemiesNow = 0;
        foreach (var h in FindObjectsByType<Signal.Combat.Health.HealthComponent>(FindObjectsSortMode.None))
            if (h.CompareTag("Enemy")) enemiesNow++;
        Debug.Log($"[Graph] Enemies present right after generation: {enemiesNow} (expect 0) {(enemiesNow == 0 ? "PASS" : "FAIL")}");

        RoomDefinition start = generator.Rooms.Count > 0 ? generator.Rooms[0] : null;
        int startSections = 0;
        if (start != null)
            foreach (var s in start.GetComponentsInChildren<Signal.Spawning.EnemySpawnSection>(true))
                if (s.gameObject.activeInHierarchy) startSections++;
        Debug.Log($"[Graph] Start room active spawn sections: {startSections} {(startSections == 0 ? "PASS — enemy-free" : "FAIL")}");

        int worstRoom = 0, sectionsFired = 0, checkpoints = 0;
        foreach (RoomDefinition room in generator.Rooms)
        {
            if (room == null) continue;
            checkpoints += room.Checkpoints.Length;

            int inRoom = 0;
            foreach (var section in room.SpawnSections)
            {
                if (!section.gameObject.activeInHierarchy) continue;
                section.Activate();
                inRoom += section.SpawnedEnemies.Count;
                sectionsFired++;
            }
            yield return null;
            worstRoom = Mathf.Max(worstRoom, inRoom);
        }

        Debug.Log($"[Graph] Spawn sections fired: {sectionsFired}, worst room: {worstRoom} enemies " +
                  $"{(sectionsFired > 0 && worstRoom > 0 && worstRoom <= 3 ? "PASS" : "FAIL")}");
        Debug.Log($"[Graph] Checkpoints in level: {checkpoints} {(checkpoints > 0 ? "PASS" : "FAIL")}");
        Debug.Log($"[Graph] RespawnManager alive: {(Signal.World.RespawnManager.Instance != null ? "PASS" : "FAIL")}");
        Debug.Log($"[Graph] AudioManager alive: {(Signal.Audio.AudioManager.Instance != null ? "PASS" : "FAIL")}");

        Debug.Log("[Graph] DONE");
        Quit();
    }

    /// <summary>Rooms with 3+ used doorways are junctions — the signature of a branch.</summary>
    private static int CountBranchRooms(LevelGenerator generator)
    {
        int branches = 0;
        foreach (RoomDefinition room in generator.Rooms)
        {
            if (room == null) continue;
            int used = 0;
            foreach (RoomConnector c in room.Connectors) if (c != null && c.IsOccupied) used++;
            if (used >= 3) branches++;
        }
        return branches;
    }

    private static int CountRotatedRooms(LevelGenerator generator)
    {
        int rotated = 0;
        foreach (RoomDefinition room in generator.Rooms)
            if (room != null && Quaternion.Angle(room.transform.rotation, Quaternion.identity) > 1f) rotated++;
        return rotated;
    }

    private static int CountOpenDoors(LevelGenerator generator)
    {
        int open = 0;
        foreach (RoomDefinition room in generator.Rooms)
        {
            if (room == null) continue;
            foreach (RoomConnector c in room.Connectors)
            {
                if (c == null || c.IsOccupied) continue;
                // Unused: the blocking panel must be back in place, or it's a hole to the void.
                var wall = (GameObject)typeof(RoomConnector)
                    .GetField("blockingWall", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(c);
                if (wall == null || !wall.activeSelf) open++;
            }
        }
        return open;
    }

    private static Bounds LevelExtent(LevelGenerator generator)
    {
        var bounds = new Bounds();
        bool first = true;
        foreach (RoomDefinition room in generator.Rooms)
        {
            if (room == null) continue;
            if (first) { bounds = room.WorldBounds; first = false; }
            else bounds.Encapsulate(room.WorldBounds);
        }
        return bounds;
    }

    private static string Fingerprint(LevelGenerator generator)
    {
        var sb = new System.Text.StringBuilder();
        foreach (RoomDefinition room in generator.Rooms)
            sb.Append(room == null ? "null" : $"{room.name}@{room.transform.position:F2}{room.transform.rotation.eulerAngles:F0}").Append(';');
        return sb.ToString();
    }

    private static object Settings(LevelGenerator g)
        => typeof(LevelGenerator).GetField("settings", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(g);

    private static void SetSeed(LevelGenerator g, int seed)
    {
        object s = Settings(g);
        typeof(GenerationSettings).GetField("useRandomSeed", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(s, true);
        typeof(GenerationSettings).GetField("randomSeed", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(s, seed);
    }

    private static void SetBranchChance(LevelGenerator g, float chance)
        => typeof(GenerationSettings).GetField("branchChance", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(Settings(g), chance);

    private static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(0);
#endif
    }
}
