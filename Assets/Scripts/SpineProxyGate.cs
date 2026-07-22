// Reference-counts SpineProxy suspension so its owners (movement while airborne/rolling, combat during the full-body standing bash) don't fight over SpineProxy.enabled when their windows overlap.
using System.Collections.Generic;
using UnityEngine;
using KevinIglesias;
using Signal.Combat;

public static class SpineProxyGate
{
    private static readonly Dictionary<SpineProxy, HashSet<object>> Suspenders =
        new Dictionary<SpineProxy, HashSet<object>>();

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

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState() => Suspenders.Clear();
}
