using System.Collections;
using System.Reflection;
using Signal.Combat.Data;
using Signal.Combat.Health;
using UnityEngine;

/// <summary>
/// TEMPORARY play-mode harness: verifies the lock-on indicator bounces and that killing the locked
/// enemy retargets the nearest survivor / releases when none remain. Deleted once it has run.
///
/// The component is left disabled so its input-driven Update never runs; the retarget path is fired
/// for real through HealthComponent.Died, and driven via reflection on the private members.
/// </summary>
public class LockOnVerifyRunner : MonoBehaviour
{
    private static readonly BindingFlags Priv = BindingFlags.NonPublic | BindingFlags.Instance;

    private IEnumerator Start()
    {
        // A main camera, since PlayerLockOn.Awake reads Camera.main.
        var camGo = new GameObject("Main Camera", typeof(Camera)) { tag = "MainCamera" };
        camGo.transform.position = new Vector3(0f, 5f, -8f);

        // The player + lock-on. Disabled so Update (which needs the input rig) never runs.
        var playerGo = new GameObject("Player");
        var indicator = GameObject.CreatePrimitive(PrimitiveType.Cube);
        indicator.name = "LockOnIndicator";

        var lockOn = playerGo.AddComponent<PlayerLockOn>();
        lockOn.enabled = false;
        lockOn.lockOnIndicator = indicator;
        lockOn.lockOnRange = 15f;
        lockOn.targetHeightOffset = 1f;
        lockOn.bounceAmplitude = 0.15f;
        lockOn.bounceSpeed = 4f;

        // Three enemies in a line: A nearest, then B, then C.
        HealthComponent a = MakeEnemy("EnemyA", new Vector3(2f, 0f, 0f));
        HealthComponent b = MakeEnemy("EnemyB", new Vector3(4f, 0f, 0f));
        HealthComponent c = MakeEnemy("EnemyC", new Vector3(6f, 0f, 0f));
        Physics.SyncTransforms();
        yield return null;

        // ── Lock onto A ───────────────────────────────────────────────────────
        Invoke(lockOn, "SetTarget", a.transform);
        yield return null;

        Transform target = Target(lockOn);
        Debug.Log($"[LockOn] Locked A: target={Name(target)} indicatorActive={indicator.activeSelf} " +
                  $"parent={indicator.transform.parent?.name} {(target == a.transform && indicator.activeSelf ? "PASS" : "FAIL")}");
        if (target != a.transform) Debug.LogError("[LockOn] Did not lock onto A.");

        // ── Bounce: localPosition.y must oscillate within the amplitude ─────────
        float min = float.MaxValue, max = float.MinValue;
        for (int i = 0; i < 40; i++)
        {
            Invoke(lockOn, "UpdateIndicator");
            float y = indicator.transform.localPosition.y;
            min = Mathf.Min(min, y);
            max = Mathf.Max(max, y);
            yield return null;
        }
        float travel = max - min;
        bool inRange = min >= 1f - 0.15f - 0.001f && max <= 1f + 0.15f + 0.001f;
        Debug.Log($"[LockOn] Bounce y range [{min:F3}, {max:F3}] travel={travel:F3} " +
                  $"{(travel > 0.05f && inRange ? "PASS — bobs within amplitude" : "FAIL")}");
        if (travel <= 0.05f) Debug.LogError("[LockOn] Indicator is not bouncing.");
        if (!inRange) Debug.LogError("[LockOn] Bounce left the configured amplitude band.");

        // ── Kill A → should retarget to the nearest survivor (B) ────────────────
        a.TakeDamage(new DamageInfo(9999f, gameObject));
        yield return null;

        target = Target(lockOn);
        bool aColliderOff = !a.GetComponent<Collider>().enabled;
        Debug.Log($"[LockOn] Killed A: target now {Name(target)} (expect EnemyB), A collider disabled={aColliderOff} " +
                  $"{(target == b.transform ? "PASS" : "FAIL")}");
        if (target != b.transform) Debug.LogError("[LockOn] Did not retarget to nearest survivor B.");
        Debug.Log($"[LockOn] Indicator reparented to new target: {(indicator.transform.parent == b.transform ? "PASS" : "FAIL")}");

        // ── Kill B → should retarget to C ───────────────────────────────────────
        b.TakeDamage(new DamageInfo(9999f, gameObject));
        yield return null;

        target = Target(lockOn);
        Debug.Log($"[LockOn] Killed B: target now {Name(target)} (expect EnemyC) {(target == c.transform ? "PASS" : "FAIL")}");
        if (target != c.transform) Debug.LogError("[LockOn] Did not retarget to C.");

        // ── Kill C (last one) → should drop the lock entirely ───────────────────
        c.TakeDamage(new DamageInfo(9999f, gameObject));
        yield return null;

        target = Target(lockOn);
        bool locked = (bool)typeof(PlayerLockOn).GetField("_locked", Priv).GetValue(lockOn);
        Debug.Log($"[LockOn] Killed last enemy: target={Name(target)} locked={locked} indicatorActive={indicator.activeSelf} " +
                  $"{(target == null && !locked && !indicator.activeSelf ? "PASS — lock released" : "FAIL")}");
        if (target != null || locked) Debug.LogError("[LockOn] Lock was not released when no enemies remained.");
        Debug.Log($"[LockOn] Indicator reparented back to player: {(indicator.transform.parent == playerGo.transform ? "PASS" : "FAIL")}");

        Debug.Log("[LockOn] DONE");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(0);
#endif
    }

    private HealthComponent MakeEnemy(string name, Vector3 pos)
    {
        var go = new GameObject(name) { tag = "Enemy" };
        go.transform.position = pos;
        go.AddComponent<BoxCollider>();
        var health = go.AddComponent<HealthComponent>();
        go.AddComponent<DeathHandler>(); // subscribes to Died first — disables colliders on death
        return health;
    }

    private static void Invoke(object target, string method, params object[] args)
        => typeof(PlayerLockOn).GetMethod(method, Priv).Invoke(target, args);

    private static Transform Target(PlayerLockOn lockOn)
        => (Transform)typeof(PlayerLockOn).GetField("_target", Priv).GetValue(lockOn);

    private static string Name(Transform t) => t != null ? t.name : "<none>";
}
