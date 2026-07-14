using System.Collections;
using UnityEngine;
using Signal.Combat.Configs;
using Signal.Combat.Data;
using Signal.Combat.Interfaces;

namespace Signal.Combat.Attacks
{
    /// <summary>
    /// Crowd-control bash: knockback only, never damage. Picks the upper-body (moving) or full-body
    /// (standing) animation variant per swing; both share the same gameplay timing.
    /// </summary>
    public sealed class BashStrategy : IAttackStrategy
    {
        private readonly BashConfigSO _config;

        public BashStrategy(BashConfigSO config)
        {
            _config = config;
        }

        public void Tick(float deltaTime) { }

        public bool CanExecute(ICombatInputSource input) => input.BashPressedThisFrame;

        public IEnumerator Execute(AttackExecutionContext ctx, ICombatInputSource input)
        {
            // Latch once for the whole swing so a mid-bash stick wiggle can't flip states.
            bool moving = input.MoveInput.magnitude > _config.movingInputThreshold
                          || (ctx.GetPlanarSpeed?.Invoke() ?? 0f) > _config.movingSpeedThreshold;
            _config.SelectVariant(moving);
            SetStandingBool(ctx, standing: !moving);
            CombatLog.Info(moving ? "Bash variant: moving" : "Bash variant: standing");

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
            CombatLog.Info($"Bash: {totalHits} target(s) knocked back");

            yield return ctx.WaitForAttackExit(_config);
        }

        // Missing parameter is already reported at startup by PlayerCombat, so skip quietly here.
        private void SetStandingBool(AttackExecutionContext ctx, bool standing)
        {
            if (ctx.Animator == null || string.IsNullOrEmpty(_config.standingBoolParameter)) return;
            if (ctx.ValidAnimatorParameters != null &&
                !ctx.ValidAnimatorParameters.Contains(_config.StandingBoolHash)) return;

            ctx.Animator.SetBool(_config.StandingBoolHash, standing);
        }
    }
}
