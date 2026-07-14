using System.Collections.Generic;
using UnityEngine;
using KevinIglesias;
using Signal.Combat;

/// <summary>
/// Reference-counts <see cref="SpineProxy"/> suspension so its owners (movement while airborne/rolling,
/// combat during the full-body standing bash) don't fight over SpineProxy.enabled when their windows
/// overlap. The proxy resumes only once every owner has released.
/// </summary>
public static class SpineProxyGate
{
    private static readonly Dictionary<SpineProxy, HashSet<object>> Suspenders =
        new Dictionary<SpineProxy, HashSet<object>>();

    /// <summary>Registers or releases <paramref name="owner"/>'s suspension request. Safe to call every frame.</summary>
    public static void SetSuspended(SpineProxy proxy, object owner, bool suspend)
    {
        if (proxy == null) return;

        if (!Suspenders.TryGetValue(proxy, out HashSet<object> owners))
        {
            if (!suspend) return;
            Suspenders[proxy] = owners = new HashSet<object>();
        }

        bool changed = suspend ? owners.Add(owner) : owners.Remove(owner);
        if (!changed) return;

        bool shouldRun = owners.Count == 0;
        if (proxy.enabled != shouldRun)
        {
            proxy.enabled = shouldRun;
            CombatLog.Info(shouldRun ? "SpineProxy resumed" : "SpineProxy suspended", proxy);
        }
    }

    // Statics outlive play sessions when domain reload is off; reset per run.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState() => Suspenders.Clear();
}
