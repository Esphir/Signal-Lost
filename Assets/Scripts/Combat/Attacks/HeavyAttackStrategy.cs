using System.Collections;
using UnityEngine;
using Signal.Combat.Configs;
using Signal.Combat.Data;
using Signal.Combat.Interfaces;

namespace Signal.Combat.Attacks
{
    /// <summary>
    /// Heavy attack supporting both a single-click instant swing and a hold-to-charge swing whose
    /// damage/range scale with charge time, entirely driven by <see cref="HeavyAttackConfigSO"/>.
    /// Swing timing rides the actual animator state via the <see cref="AttackExecutionContext"/>
    /// helpers, so it stays in sync with the clip regardless of its length or playback speed.
    /// </summary>
    public sealed class HeavyAttackStrategy : IAttackStrategy
    {
        private readonly HeavyAttackConfigSO _config;

        public HeavyAttackStrategy(HeavyAttackConfigSO config)
        {
            _config = config;
        }

        public void Tick(float deltaTime) { /* no persistent state between swings */ }

        public bool CanExecute(ICombatInputSource input) => input.HeavyAttackPressedThisFrame;

        public IEnumerator Execute(AttackExecutionContext ctx, ICombatInputSource input)
        {
            float chargeRatio = 1f;

            if (_config.mode == HeavyAttackMode.HoldToCharge)
            {
                CombatLog.Info($"Heavy attack charging… (hold up to {_config.maxChargeTime:0.##}s, release to swing)");

                float held = 0f;
                while (input.HeavyAttackHeld && held < _config.maxChargeTime)
                {
                    held += Time.deltaTime;
                    ctx.OnChargeProgress?.Invoke(held / _config.maxChargeTime);
                    yield return null;
                }
                chargeRatio = Mathf.Clamp01(held / _config.maxChargeTime);

                // If the player kept holding past full charge, wait for release before swinging.
                while (input.HeavyAttackHeld)
                    yield return null;

                ctx.OnChargeProgress?.Invoke(0f);
                CombatLog.Info($"Heavy attack released at {chargeRatio:P0} charge");
            }

            float damage = Mathf.Lerp(_config.damage * _config.minChargeDamageMultiplier, _config.damage, chargeRatio);
            float radius = Mathf.Lerp(_config.hitRadius * _config.minChargeRangeMultiplier, _config.hitRadius, chargeRatio);

            ctx.SetAttackTrigger(_config);
            ctx.ApplyRootMotion?.Invoke(ctx.Origin.forward * _config.lungeDistance);

            yield return ctx.WaitForImpactPhase(_config);

            int totalHits = 0;
            bool feedbackFired = false;
            ctx.Resolver.BeginSwing();

            while (true)
            {
                Vector3 hitCenter = ctx.Origin.position + ctx.Origin.forward * _config.hitOffset + Vector3.up * 0.8f;
                int count = ctx.HitDetector.Detect(hitCenter, radius, ctx.HitMask);
                int newHits = ctx.Resolver.ApplyDamage(ctx.HitDetector.Buffer, count, damage, ctx.Instigator, hitCenter, isHeavy: true);
                totalHits += newHits;

                if (newHits > 0 && !feedbackFired)
                {
                    feedbackFired = true;
                    ctx.TriggerHitStop?.Invoke(_config.hitStopDuration);
                    ctx.TriggerCameraShake?.Invoke(_config.cameraShakeAmount, _config.cameraShakeDuration);
                }

                if (!ctx.IsInActiveWindow(_config)) break;
                yield return null;
            }

            if (totalHits > 0) ctx.OnAttackLanded?.Invoke(totalHits);
            CombatLog.Info($"Heavy attack: {totalHits} target(s) damaged for {damage:0.#} (radius {radius:0.##})");

            yield return ctx.WaitForAttackExit(_config);
        }
    }
}
