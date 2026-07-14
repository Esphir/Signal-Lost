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
    /// Bundles the collaborators an <see cref="IAttackStrategy"/> needs to run without giving it a
    /// direct reference to any MonoBehaviour. Built once by the composition root (PlayerCombat) and
    /// reused for every attack — callbacks are the seam that keeps strategies decoupled from
    /// concrete systems like camera shake or root motion.
    /// </summary>
    public sealed class AttackExecutionContext
    {
        public Transform Origin;
        public GameObject Instigator;
        public Animator Animator;
        public LayerMask HitMask;
        public IAttackHitDetector HitDetector;
        public CombatHitResolver Resolver;

        /// <summary>
        /// Name-hashes of every parameter on <see cref="Animator"/>, cached once at startup by
        /// PlayerCombat. Used by <see cref="SetAttackTrigger"/> to fail loudly instead of letting
        /// Animator.SetTrigger silently no-op on a missing parameter.
        /// </summary>
        public HashSet<int> ValidAnimatorParameters;

        /// <summary>
        /// Every attack trigger hash in the moveset, set by PlayerCombat at startup. Cleared before
        /// each swing so a trigger queued by spam-clicking (but not consumed by a transition yet)
        /// can't fire a phantom extra swing later.
        /// </summary>
        public int[] AttackTriggerHashes;

        /// <summary>Move the attacker by a world-space delta (attack lunge / root motion).</summary>
        public Action<Vector3> ApplyRootMotion;

        /// <summary>Attacker's horizontal speed (m/s); lets variant attacks tell moving from standing.</summary>
        public Func<float> GetPlanarSpeed;

        /// <summary>Resolves a stat's final value (base + run modifiers). Unwired, base values pass through.</summary>
        public Func<StatType, float, float> GetStat;

        public float ResolveStat(StatType stat, float baseValue)
            => GetStat?.Invoke(stat, baseValue) ?? baseValue;

        /// <summary>Damage multiplier applied on a critical hit. Set by the composition root.</summary>
        public float CriticalMultiplier = 2f;

        /// <summary>Hook for crit VFX/SFX/UI, invoked with the hit position when a crit connects.</summary>
        public Action<Vector3> OnCriticalHit;

        /// <summary>Reports damage dealt to targets (per sweep). Life steal and similar hook in here.</summary>
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
        /// Fires the attack's animator trigger with validation and logging. An empty trigger name
        /// means "no animation for this attack" and is skipped quietly; a trigger that doesn't
        /// exist on the Animator is a configuration error and logged loudly every swing.
        /// </summary>
        /// <returns>True if the trigger was actually set.</returns>
        public bool SetAttackTrigger(AttackConfigBaseSO config)
        {
            if (string.IsNullOrEmpty(config.animatorTrigger))
                return false; // intentionally animation-less attack; reported once at startup

            if (Animator == null)
                return false; // missing animator already warned about at startup

            if (ValidAnimatorParameters != null && !ValidAnimatorParameters.Contains(config.AnimatorTriggerHash))
            {
                Debug.LogError(
                    $"[Combat] Animator has no trigger '{config.animatorTrigger}' (attack '{config.name}') — animation will NOT play.",
                    Animator);
                return false;
            }

            // Clear any trigger left queued by an earlier press so exactly one attack trigger is
            // pending — otherwise a stale trigger causes an unrequested extra swing after this one.
            if (AttackTriggerHashes != null)
                foreach (int hash in AttackTriggerHashes)
                    Animator.ResetTrigger(hash);

            Animator.SetTrigger(config.AnimatorTriggerHash);
            CombatLog.Info($"Animator trigger '{config.animatorTrigger}' fired ({config.name})", Animator);
            return true;
        }

        // ── Animation-driven timing ───────────────────────────────────────

        // State hash → owning layer (-1 = not found). Keyed by hash so per-swing variants like the
        // bash's moving/standing states each cache independently. Attacks may live on different layers.
        private readonly Dictionary<int, int> _stateLayerCache = new Dictionary<int, int>();

        /// <summary>Finds (and caches) the animator layer whose state machine contains the config's active state.</summary>
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
        /// Reads the normalized time of the attack's active state on whichever layer owns it,
        /// checking both the current state and the cross-fade target so entry transitions are
        /// covered. Returns false whenever the state isn't active.
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
        /// Waits until the attack's clip reaches its impact frame (<see cref="AttackConfigBaseSO.impactNormalizedTime"/>).
        /// Falls back to the config's fixed startupTime when no animator state drives this attack
        /// or the state never activates (missing trigger/state — already reported loudly).
        /// </summary>
        public IEnumerator WaitForImpactPhase(AttackConfigBaseSO config)
        {
            if (!CanDriveByAnimation(config))
            {
                yield return new WaitForSeconds(config.startupTime);
                yield break;
            }

            // The trigger was set this frame; the animator processes it after this script update,
            // so allow a few frames for the entry transition to actually begin.
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

        /// <summary>
        /// True while the clip is inside the attack's active frames [impact, activeEnd). Always
        /// false in fallback mode, which degrades the active window to a single hit pulse.
        /// </summary>
        public bool IsInActiveWindow(AttackConfigBaseSO config)
            => TryGetAttackNormalizedTime(config, out float t) && t < config.activeEndNormalizedTime;

        /// <summary>
        /// Waits until the clip reaches <see cref="AttackConfigBaseSO.exitNormalizedTime"/> (or the
        /// state ends early), at which point control returns and the next attack may start.
        /// Falls back to the config's fixed recoveryTime without animation data.
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
