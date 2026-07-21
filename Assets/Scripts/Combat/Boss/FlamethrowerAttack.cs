using System.Collections;
using UnityEngine;

namespace Signal.Combat.Boss
{
    /// <summary>
    /// Attack 1 — Flamethrower. The boss faces the player, its nozzle glows through a short windup, then
    /// it fires a continuous cone that slowly sweeps to one side while drifting toward the player, and
    /// drips small burning patches along the ground. It's a positioning tool: the safe answer is to move
    /// opposite the sweep, step out of range, or get behind it.
    ///
    /// The burn is measured from the boss outward, with a splash covering its own footprint — a cone hung
    /// off the nozzle would pinch to nothing exactly where a melee player stands, making the safest place
    /// in the room the one place a flamethrower should never be safe.
    /// </summary>
    public sealed class FlamethrowerAttack : BossAttack
    {
        [Header("Aim / Windup")]
        [SerializeField, Min(0f)] private float faceTime = 0.5f;
        [SerializeField, Min(0.1f)] private float windup = 0.7f;

        [Header("Flame")]
        [SerializeField, Min(0.5f)] private float fireDuration = 2.2f;
        [SerializeField, Min(1f)] private float range = 9f;
        [SerializeField, Min(5f)] private float coneHalfAngle = 24f;
        [SerializeField, Min(10f)] private float sweepArc = 75f;
        [SerializeField, Min(1f)] private float damagePerSecond = 22f;
        [SerializeField] private Vector3 nozzleOffset = new Vector3(0f, 1.2f, 0.6f);

        [SerializeField, Min(0f)]
        [Tooltip("Splash around the boss that burns too — closes the safe pocket a bare cone leaves at its feet. Only ahead of it; behind is still safe.")]
        private float splashRadius = 2.6f;

        [SerializeField, Min(0f)]
        [Tooltip("Degrees per second the sweep drifts toward the player while firing. 0 = fully committed to its opening aim.")]
        private float trackingSpeed = 30f;

        [Header("Preferred range")]
        [SerializeField, Min(0f)] private float idealDistance = 7f;

        [Header("Burning ground")]
        [SerializeField, Min(0f)] private float patchInterval = 0.35f;
        [SerializeField, Min(0.3f)] private float patchRadius = 1.4f;
        [SerializeField, Min(0f)] private float patchDps = 10f;
        [SerializeField, Min(0.5f)] private float patchLifetime = 3.5f;

        public override float WeightAt(float distance, BossContext ctx)
        {
            // Best at mid range where the whole cone lands; useless if the player is outside its reach.
            if (distance > range + 3f) return 0.15f;
            return 3f + Mathf.Max(0f, 4f - Mathf.Abs(distance - idealDistance));
        }

        protected override IEnumerator Execute(BossContext ctx)
        {
            const float tick = 0.2f;
            float dps = damagePerSecond;
            float duration = fireDuration * ctx.FlameDurationMultiplier;

            // Face the player during the windup so the aim is honest before flames appear.
            ctx.Anim?.Anticipate(0.3f); // squeeze down like a gripped bottle — pressure building, not growing
            yield return FaceOverTime(ctx, faceTime);

            var jet = new GameObject("FlameJet");
            jet.transform.SetParent(ctx.Boss, false);
            jet.transform.localPosition = nozzleOffset;
            jet.transform.localRotation = Quaternion.Euler(12f, 0f, 0f); // tilt down so it licks the ground
            ParticleSystem flames = FlameVfx.BuildJet(jet, range, coneHalfAngle);
            ParticleSystem.EmissionModule emission = flames.emission;

            // Nozzle "glow": a weak flicker during windup, then full blast.
            emission.rateOverTime = 12f;
            flames.Play();
            yield return Wait(windup, ctx);
            emission.rateOverTime = 120f;

            ctx.Anim?.Pulse(0.4f);       // the squeeze releases as the flame catches
            ctx.Anim?.Anticipate(0.12f); // then stay lightly compressed while it sprays

            Quaternion baseRot = FacingPlayer(ctx);
            float side = Random.value < 0.5f ? -1f : 1f;
            var ticker = new FlameTicker();
            float patchTimer = 0f;

            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = t / duration;

                // The sweep is the attack, but a fixed arc is trivially walked out of — so the aim drifts
                // toward the player underneath it. Slow enough that running still beats it.
                baseRot = Quaternion.RotateTowards(baseRot, FacingPlayer(ctx), trackingSpeed * Time.deltaTime);
                ctx.Boss.rotation = baseRot * Quaternion.Euler(0f, side * sweepArc * (k - 0.5f), 0f);

                // Damage is measured from the boss, not from the nozzle out in front of it: the cone has to
                // start where the boss is, or standing on top of it is safe.
                Vector3 origin = ctx.Boss.position;
                Vector3 forward = ctx.Boss.forward;

                bool burning = ctx.Player != null &&
                               FlameDamage.InFlame(origin, forward, coneHalfAngle, range, splashRadius, ctx.Player.position);
                float due = ticker.Tick(burning, Time.deltaTime, tick);
                if (due > 0f && ctx.Player != null) ctx.DamagePlayer(dps * due, ctx.Player.position);

                patchTimer -= Time.deltaTime;
                if (patchTimer <= 0f && patchDps > 0f)
                {
                    patchTimer = patchInterval;
                    Vector3 spot = origin + forward * (range * Random.Range(0.4f, 0.85f));
                    spot.y = ctx.Boss.position.y;
                    BurningGround.Spawn(spot, patchRadius, ctx.ScaleDamage(patchDps),
                                        patchLifetime * ctx.FlameDurationMultiplier, ctx.Instigator);
                }

                yield return null;
            }

            float last = ticker.Flush();
            if (last > 0f && ctx.Player != null) ctx.DamagePlayer(dps * last, ctx.Player.position);

            Destroy(jet);
            ctx.Anim?.Relax();
        }

        private IEnumerator FaceOverTime(BossContext ctx, float seconds)
        {
            if (seconds <= 0f) { ctx.Boss.rotation = FacingPlayer(ctx); yield break; }
            Quaternion from = ctx.Boss.rotation;
            Quaternion to = FacingPlayer(ctx);
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                ctx.Boss.rotation = Quaternion.Slerp(from, to, t / seconds);
                to = FacingPlayer(ctx); // keep tracking while turning
                yield return null;
            }
            ctx.Boss.rotation = FacingPlayer(ctx);
        }

        private static Quaternion FacingPlayer(BossContext ctx)
        {
            if (ctx.Player == null) return ctx.Boss.rotation;
            Vector3 dir = ctx.Player.position - ctx.Boss.position; dir.y = 0f;
            return dir.sqrMagnitude < 0.001f ? ctx.Boss.rotation : Quaternion.LookRotation(dir);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, range);
        }
    }
}
