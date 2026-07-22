// Bundles the collaborators an IAttackStrategy needs without giving it a direct reference to any MonoBehaviour.
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
    public sealed class AttackExecutionContext
    {
        public Transform Origin;
        public GameObject Instigator;
        public Animator Animator;
        public LayerMask HitMask;
        public IAttackHitDetector HitDetector;
        public CombatHitResolver Resolver;

        public HashSet<int> ValidAnimatorParameters;

        public int[] AttackTriggerHashes;

        public Action<Vector3> ApplyRootMotion;

        public Func<float> GetPlanarSpeed;

        public Func<StatType, float, float> GetStat;

        public float ResolveStat(StatType stat, float baseValue)
            => GetStat?.Invoke(stat, baseValue) ?? baseValue;

        public float CriticalMultiplier = 2f;

        public Action<Vector3> OnCriticalHit;

        public Action<float> OnDamageDealt;

        public Action OnImpact;

        public float RollCritical(float damage, out bool isCritical)
        {
            float chance = ResolveStat(StatType.CritChance, 0f);
            isCritical = chance > 0f && UnityEngine.Random.value * 100f < chance;
            return isCritical ? damage * CriticalMultiplier : damage;
        }

        public Action<float> TriggerHitStop;

        public Action<float, float> TriggerCameraShake;

        public Action<float> OnChargeProgress;

        public Action<int> OnAttackLanded;

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

            if (AttackTriggerHashes != null)
                foreach (int hash in AttackTriggerHashes)
                    Animator.ResetTrigger(hash);

            Animator.SetTrigger(config.AnimatorTriggerHash);
            CombatLog.Info($"Animator trigger '{config.animatorTrigger}' fired ({config.name})", Animator);
            return true;
        }

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

        public IEnumerator WaitForImpactPhase(AttackConfigBaseSO config)
        {
            if (!CanDriveByAnimation(config))
            {
                yield return new WaitForSeconds(config.startupTime);
                yield break;
            }

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

        public bool IsInActiveWindow(AttackConfigBaseSO config)
            => TryGetAttackNormalizedTime(config, out float t) && t < config.activeEndNormalizedTime;

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
