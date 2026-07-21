using System.Collections;
using UnityEngine;

namespace Signal.Combat.Boss
{
    /// <summary>
    /// Attack 1 — Flamethrower. The boss faces the player, its nozzle glows through a short windup, then
    /// it fires a continuous cone that slowly sweeps to one side. Damage ticks while the player stands in
    /// the cone, and it drips small burning patches along the ground. It's a positioning tool: the safe
    /// answer is always to move opposite the sweep or step out of range.
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
            float dpsTickTimer = 0f, patchTimer = 0f;

            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = t / duration;
                ctx.Boss.rotation = baseRot * Quaternion.Euler(0f, side * sweepArc * (k - 0.5f), 0f);

                Vector3 origin = jet.transform.position;
                Vector3 forward = ctx.Boss.forward;

                dpsTickTimer -= Time.deltaTime;
                if (dpsTickTimer <= 0f)
                {
                    dpsTickTimer = tick;
                    if (ctx.Player != null && FlameDamage.InCone(origin, forward, coneHalfAngle, range, ctx.Player.position))
                        ctx.DamagePlayer(dps * tick, ctx.Player.position);
                }

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
