using System.Collections.Generic;
using UnityEngine;
using Signal.Combat.Data;
using Signal.Combat.Interfaces;

namespace Signal.Combat.Detection
{
    /// <summary>
    /// Applies damage/knockback to a set of detected colliders, deduplicating so a target with
    /// multiple colliders (e.g. several hitboxes on one rig) is only affected once per swing —
    /// even when the swing's active window sweeps the hit volume across several frames.
    /// Call <see cref="BeginSwing"/> once at the start of each swing's active window.
    /// A plain (non-MonoBehaviour) class owned by the attacking component — no singleton needed.
    /// </summary>
    public sealed class CombatHitResolver
    {
        // Reused across swings to avoid per-swing allocations.
        private readonly HashSet<IDamageable> _damagedThisSwing = new HashSet<IDamageable>();
        private readonly HashSet<IKnockbackable> _knockedThisSwing = new HashSet<IKnockbackable>();

        /// <summary>
        /// Resets per-swing target tracking. The following ApplyDamage/ApplyKnockback sweeps —
        /// however many frames the active window spans — will hit each unique target at most once.
        /// </summary>
        public void BeginSwing()
        {
            _damagedThisSwing.Clear();
            _knockedThisSwing.Clear();
        }

        /// <summary>Deals <paramref name="damage"/> to every unique <see cref="IDamageable"/> found in the buffer.</summary>
        /// <returns>Number of unique targets hit this sweep.</returns>
        public int ApplyDamage(Collider[] buffer, int count, float damage, GameObject instigator, Vector3 hitPoint = default, bool isHeavy = false, bool isCritical = false)
        {
            int hits = 0;

            for (int i = 0; i < count; i++)
            {
                var damageable = buffer[i].GetComponentInParent<IDamageable>();
                if (damageable == null)
                {
                    CombatLog.Warn($"'{buffer[i].name}' is on the hit mask but has no IDamageable in its parents — no damage applied.", buffer[i]);
                    continue;
                }
                if (!damageable.IsAlive) continue;
                if (!_damagedThisSwing.Add(damageable)) continue;

                Vector3 hitDir = (buffer[i].transform.position - instigator.transform.position).normalized;
                damageable.TakeDamage(new DamageInfo(damage, instigator, hitPoint, hitDir, isHeavy, isCritical));
                CombatLog.Info($"Dealt {damage:0.#} damage to '{(damageable as Component)?.name}'{(isCritical ? " (CRIT)" : "")}.", buffer[i]);
                hits++;
            }

            return hits;
        }

        /// <summary>Applies a knockback force to every unique <see cref="IKnockbackable"/> found in the buffer.</summary>
        /// <returns>Number of unique targets affected this sweep.</returns>
        public int ApplyKnockback(Collider[] buffer, int count, Vector3 force, ForceMode forceMode)
        {
            int hits = 0;

            for (int i = 0; i < count; i++)
            {
                var knockbackable = buffer[i].GetComponentInParent<IKnockbackable>();
                if (knockbackable == null)
                {
                    CombatLog.Warn($"'{buffer[i].name}' is on the hit mask but has no IKnockbackable in its parents — bash has no effect on it.", buffer[i]);
                    continue;
                }
                if (!_knockedThisSwing.Add(knockbackable)) continue;

                knockbackable.ApplyKnockback(force, forceMode);
                CombatLog.Info($"Knockback applied to '{(knockbackable as Component)?.name}' (impulse {force.magnitude:0.#}).", buffer[i]);
                hits++;
            }

            return hits;
        }
    }
}
