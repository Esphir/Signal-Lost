// Crowd-control bash: knockback only, never damage.
using System.Collections;
using UnityEngine;
using Signal.Combat.Configs;
using Signal.Combat.Data;
using Signal.Combat.Interfaces;

namespace Signal.Combat.Attacks
{
    public sealed class BashStrategy : IAttackStrategy
    {
        private readonly BashConfigSO _config;
        private float _cooldownRemaining;

        public BashStrategy(BashConfigSO config)
        {
            _config = config;
        }

        public void Tick(float deltaTime)
        {
            if (_cooldownRemaining > 0f) _cooldownRemaining -= deltaTime;
        }

        public bool CanExecute(ICombatInputSource input)
            => _cooldownRemaining <= 0f && input.BashPressedThisFrame;

        public IEnumerator Execute(AttackExecutionContext ctx, ICombatInputSource input)
        {
            _cooldownRemaining = _config.cooldown;

            bool moving = input.MoveInput.magnitude > _config.movingInputThreshold
                          || (ctx.GetPlanarSpeed?.Invoke() ?? 0f) > _config.movingSpeedThreshold;
            _config.SelectVariant(moving);
            SetStandingBool(ctx, standing: !moving);
            CombatLog.Info(moving ? "Bash variant: moving" : "Bash variant: standing");

            ctx.SetAttackTrigger(_config);

            yield return ctx.WaitForImpactPhase(_config);
            ctx.OnImpact?.Invoke();

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

            if (totalHits > 0)
            {
                ctx.OnAttackLanded?.Invoke(totalHits);
                ctx.OnBashConnected?.Invoke(_config.iFrameDuration);
            }
            CombatLog.Info($"Bash: {totalHits} target(s) knocked back");

            yield return ctx.WaitForAttackExit(_config);
        }

        private void SetStandingBool(AttackExecutionContext ctx, bool standing)
        {
            if (ctx.Animator == null || string.IsNullOrEmpty(_config.standingBoolParameter)) return;
            if (ctx.ValidAnimatorParameters != null &&
                !ctx.ValidAnimatorParameters.Contains(_config.StandingBoolHash)) return;

            ctx.Animator.SetBool(_config.StandingBoolHash, standing);
        }
    }
}
