using System.Collections;
using UnityEngine;
using Signal.Combat.Configs;
using Signal.Combat.Data;
using Signal.Combat.Interfaces;

namespace Signal.Combat.Attacks
{
    /// <summary>
    /// Crowd-control kick. Deals zero damage — it only ever calls
    /// <see cref="Signal.Combat.Detection.CombatHitResolver.ApplyKnockback"/>, never ApplyDamage.
    /// The kick has no animator state yet, so it runs on the config's fallback timing; give it a
    /// state name in <see cref="KickConfigSO"/> once a clip exists and it becomes animation-synced
    /// automatically, like the other attacks.
    /// </summary>
    public sealed class KickStrategy : IAttackStrategy
    {
        private readonly KickConfigSO _config;

        public KickStrategy(KickConfigSO config)
        {
            _config = config;
        }

        public void Tick(float deltaTime) { }

        public bool CanExecute(ICombatInputSource input) => input.KickPressedThisFrame;

        public IEnumerator Execute(AttackExecutionContext ctx, ICombatInputSource input)
        {
            ctx.SetAttackTrigger(_config);

            yield return ctx.WaitForImpactPhase(_config);

            int totalHits = 0;
            bool feedbackFired = false;
            ctx.Resolver.BeginSwing();

            while (true)
            {
                Vector3 hitCenter = ctx.Origin.position + ctx.Origin.forward * _config.hitOffset + Vector3.up * 0.8f;
                int count = ctx.HitDetector.Detect(hitCenter, _config.hitRadius, ctx.HitMask);

                Vector3 force = ctx.Origin.forward * _config.forwardForce + Vector3.up * _config.upwardForce;
                int newHits = ctx.Resolver.ApplyKnockback(ctx.HitDetector.Buffer, count, force, _config.forceMode);
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
            CombatLog.Info($"Kick: {totalHits} target(s) knocked back (kick deals no damage by design)");

            yield return ctx.WaitForAttackExit(_config);
        }
    }
}
