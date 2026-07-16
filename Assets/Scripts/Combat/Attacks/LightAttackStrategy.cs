using System.Collections;
using UnityEngine;
using Signal.Combat.Configs;
using Signal.Combat.Data;
using Signal.Combat.Interfaces;
using Signal.Stats;

namespace Signal.Combat.Attacks
{
    /// <summary>
    /// Fast melee attack. Owns a <see cref="ComboSequencer"/> so repeated presses walk through the
    /// configured combo chain; expanding the combo requires only adding more
    /// <see cref="LightAttackConfigSO"/> assets, not touching this class.
    /// Timing rides the actual animator state (impact/active/exit as normalized clip time) via the
    /// <see cref="AttackExecutionContext"/> helpers.
    /// </summary>
    public sealed class LightAttackStrategy : IAttackStrategy
    {
        private readonly ComboSequencer _combo;

        public LightAttackStrategy(LightAttackConfigSO firstStep)
        {
            _combo = new ComboSequencer(firstStep);
        }

        public void Tick(float deltaTime) => _combo.Tick(deltaTime);

        public bool CanExecute(ICombatInputSource input) => input.AttackPressedThisFrame;

        public IEnumerator Execute(AttackExecutionContext ctx, ICombatInputSource input)
        {
            LightAttackConfigSO step = _combo.CurrentStep;
            float damage = ctx.RollCritical(ctx.ResolveStat(StatType.AttackDamage, step.damage), out bool isCritical);

            ctx.SetAttackTrigger(step);
            ctx.ApplyRootMotion?.Invoke(ctx.Origin.forward * step.lungeDistance);

            yield return ctx.WaitForImpactPhase(step);
            ctx.OnImpact?.Invoke();

            int totalHits = 0;
            bool feedbackFired = false;
            ctx.Resolver.BeginSwing();

            // Active frames: sweep the hit volume every frame until the clip passes the
            // active-end mark (degrades to a single pulse without animation data).
            while (true)
            {
                Vector3 hitCenter = ctx.Origin.position + ctx.Origin.forward * step.hitOffset + Vector3.up * 0.8f;
                int count = ctx.HitDetector.Detect(hitCenter, step.hitRadius, ctx.HitMask);
                int newHits = ctx.Resolver.ApplyDamage(ctx.HitDetector.Buffer, count, damage, ctx.Instigator, hitCenter, isCritical: isCritical);
                totalHits += newHits;

                if (newHits > 0)
                {
                    ctx.OnDamageDealt?.Invoke(newHits * damage);
                    if (!feedbackFired)
                    {
                        feedbackFired = true;
                        if (isCritical) ctx.OnCriticalHit?.Invoke(hitCenter);
                        ctx.TriggerHitStop?.Invoke(step.hitStopDuration);
                        ctx.TriggerCameraShake?.Invoke(step.cameraShakeAmount, step.cameraShakeDuration);
                    }
                }

                if (!ctx.IsInActiveWindow(step)) break;
                yield return null;
            }

            if (totalHits > 0) ctx.OnAttackLanded?.Invoke(totalHits);
            CombatLog.Info($"Light attack '{step.attackName}': {totalHits} target(s) damaged during active window");

            yield return ctx.WaitForAttackExit(step);

            _combo.Advance();
        }
    }
}
