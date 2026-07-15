using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Signal.Combat.Configs;
using Signal.Combat.Interfaces;
using Signal.Combat.Detection;
using Signal.Stats;

namespace Signal.Combat.Data
{
    /// <summary>
    /// Bundles the collaborators an <see cref="IAttackStrategy"/> needs without giving it a direct
    /// reference to any MonoBehaviour. Built once by PlayerCombat and reused for every attack;
    /// callbacks keep strategies decoupled from concrete systems like camera shake or root motion.
    /// </summary>
    public sealed class AttackExecutionContext
    {
        public Transform Origin;
        public GameObject Instigator;
        public Animator Animator;
        public LayerMask HitMask;
        public IAttackHitDetector HitDetector;
        public CombatHitResolver Resolver;

        /// <summary>Hashes of every animator parameter, so <see cref="SetAttackTrigger"/> can fail loudly on a missing one.</summary>
        public HashSet<int> ValidAnimatorParameters;

        /// <summary>Every attack trigger hash; cleared before each swing so a queued trigger can't fire a phantom swing.</summary>
        public int[] AttackTriggerHashes;

        /// <summary>Move the attacker by a world-space delta (attack lunge / root motion).</summary>
        public Action<Vector3> ApplyRootMotion;

        /// <summary>Attacker's horizontal speed (m/s); lets variant attacks tell moving from standing.</summary>
        public Func<float> GetPlanarSpeed;

        /// <summary>Resolves a stat's final value (base + run modifiers). Unwired, base values pass through.</summary>
        public Func<StatType, float, float> GetStat;

        public float ResolveStat(StatType stat, float baseValue)
            => GetStat?.Invoke(stat, baseValue) ?? baseValue;

        /// <summary>Damage multiplier applied on a critical hit.</summary>
        public float CriticalMultiplier = 2f;

        /// <summary>Hook for crit VFX/SFX/UI, invoked with the hit position when a crit connects.</summary>
        public Action<Vector3> OnCriticalHit;

        /// <summary>Reports damage dealt per sweep. Life steal and similar hook in here.</summary>
        public Action<float> OnDamageDealt;

        /// <summary>Rolls the attacker's crit chance (a 0–100 stat) and scales the damage on success.</summary>
        public float RollCritical(float damage, out bool isCritical)
        {
            float chance = ResolveStat(StatType.CritChance, 0f);
            isCritical = chance > 0f && UnityEngine.Random.value * 100f < chance;
            return isCritical ? damage * CriticalMultiplier : damage;
        }

        /// <summary>Trigger a brief time-scale hit-stop of the given duration (seconds, unscaled).</summary>
        public Action<float> TriggerHitStop;

        /// <summary>Trigger camera shake: (amount, duration).</summary>
        public Action<float, float> TriggerCameraShake;

        /// <summary>Called every frame while a heavy attack is charging, with ratio 0..1.</summary>
        public Action<float> OnChargeProgress;

        /// <summary>Called when an attack connects, with the number of unique targets hit.</summary>
        public Action<int> OnAttackLanded;

        /// <summary>
        /// Fires the attack's animator trigger with validation. An empty trigger name means the
        /// attack is intentionally animation-less and is skipped quietly; a trigger missing from the
        /// Animator is a configuration error and logged every swing.
        /// </summary>
        public bool SetAttackTrigger(AttackConfigBaseSO config)
        {
            if (string.IsNullOrEmpty(config.animatorTrigger))
                return false;

            if (Animator == null)
                return false;

            if (ValidAnimatorParameters != null && !ValidAnimatorParameters.Contains(config.AnimatorTriggerHash))
            {
                Debug.LogError(
                    $"[Combat] Animator has no trigger '{config.animatorTrigger}' (attack '{config.name}') — animation will NOT play.",
                    Animator);
                return false;
            }

            // Clear any trigger left queued by an earlier press, or a stale one fires an extra swing.
            if (AttackTriggerHashes != null)
                foreach (int hash in AttackTriggerHashes)
                    Animator.ResetTrigger(hash);

            Animator.SetTrigger(config.AnimatorTriggerHash);
            CombatLog.Info($"Animator trigger '{config.animatorTrigger}' fired ({config.name})", Animator);
            return true;
        }

        // State hash → owning layer (-1 = not found). Keyed by hash so per-swing variants like the
        // bash's moving/standing states each cache independently.
        private readonly Dictionary<int, int> _stateLayerCache = new Dictionary<int, int>();

        private int ResolveAttackLayer(AttackConfigBaseSO config)
        {
            if (Animator == null || !config.HasAnimatorState) return -1;

            int stateHash = config.ActiveAnimatorStateHash;
            if (_stateLayerCache.TryGetValue(stateHash, out int cached)) return cached;

            int found = -1;
            for (int layer = 0; layer < Animator.layerCount; layer++)
            {
                if (Animator.HasState(layer, stateHash)) { found = layer; break; }
            }

            _stateLayerCache[stateHash] = found;
            return found;
        }

        private bool CanDriveByAnimation(AttackConfigBaseSO config)
            => Animator != null && config.HasAnimatorState && ResolveAttackLayer(config) >= 0;

        /// <summary>
        /// Reads the normalized time of the attack's active state, checking both the current state
        /// and the cross-fade target so entry transitions are covered. False when the state isn't active.
        /// </summary>
        public bool TryGetAttackNormalizedTime(AttackConfigBaseSO config, out float normalizedTime)
        {
            normalizedTime = 0f;
            int layer = ResolveAttackLayer(config);
            if (layer < 0) return false;

            AnimatorStateInfo current = Animator.GetCurrentAnimatorStateInfo(layer);
            if (current.shortNameHash == config.ActiveAnimatorStateHash)
            {
                normalizedTime = current.normalizedTime;
                return true;
            }

            AnimatorStateInfo next = Animator.GetNextAnimatorStateInfo(layer);
            if (next.shortNameHash == config.ActiveAnimatorStateHash)
            {
                normalizedTime = next.normalizedTime;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Waits until the clip reaches its impact frame. Falls back to the config's fixed
        /// startupTime when no animator state drives the attack or the state never activates.
        /// </summary>
        public IEnumerator WaitForImpactPhase(AttackConfigBaseSO config)
        {
            if (!CanDriveByAnimation(config))
            {
                yield return new WaitForSeconds(config.startupTime);
                yield break;
            }

            // The animator processes the trigger after this script update, so allow the entry
            // transition a few frames to begin before falling back.
            const float enterTimeout = 0.5f;
            float waited = 0f;
            while (!TryGetAttackNormalizedTime(config, out _))
            {
                waited += Time.deltaTime;
                if (waited >= enterTimeout)
                {
                    CombatLog.Warn($"'{config.name}': animator state '{config.ActiveAnimatorStateName}' never became active — using fixed startup time instead.", Animator);
                    yield return new WaitForSeconds(config.startupTime);
                    yield break;
                }
                yield return null;
            }

            while (TryGetAttackNormalizedTime(config, out float t) && t < config.impactNormalizedTime)
                yield return null;
        }

        /// <summary>True while the clip is inside the attack's active frames [impact, activeEnd).</summary>
        public bool IsInActiveWindow(AttackConfigBaseSO config)
            => TryGetAttackNormalizedTime(config, out float t) && t < config.activeEndNormalizedTime;

        /// <summary>
        /// Waits until the clip reaches its exit time (or the state ends early), then control returns
        /// and the next attack may start. Falls back to the config's fixed recoveryTime without animation.
        /// </summary>
        public IEnumerator WaitForAttackExit(AttackConfigBaseSO config)
        {
            if (!CanDriveByAnimation(config))
            {
                yield return new WaitForSeconds(config.recoveryTime);
                yield break;
            }

            while (TryGetAttackNormalizedTime(config, out float t) && t < config.exitNormalizedTime)
                yield return null;
        }
    }
}
