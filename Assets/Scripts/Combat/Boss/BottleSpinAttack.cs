// Attack 2 — Bottle Spin Flame Spray (the signature).
using System.Collections;
using Signal.Combat.Telegraphs;
using UnityEngine;

namespace Signal.Combat.Boss
{
    public sealed class BottleSpinAttack : BossAttack
    {
        [Header("Telegraph")]
        [SerializeField, Min(0.3f)] private float telegraph = 1.4f;
        [SerializeField] private Color telegraphColor = new Color(1f, 0.45f, 0.1f, 1f);

        [Header("Spin")]
        [SerializeField, Min(0.5f)] private float tipOverTime = 0.45f;
        [SerializeField, Min(0.5f)] private float spinDuration = 2.6f;
        [SerializeField, Min(1)] private int rotations = 2;
        [SerializeField, Min(0.3f)] private float uprightTime = 0.55f;

        [Header("Flame")]
        [SerializeField, Min(1f)] private float range = 8f;
        [SerializeField, Min(5f)] private float coneHalfAngle = 20f;
        [SerializeField, Min(1f)] private float damagePerSecond = 26f;

        [SerializeField]
        [Tooltip("Where the flame leaves the cap while lying down: x sideways, y above the floor, z along the spray.")]
        private Vector3 nozzleOffset = new Vector3(0f, 0.4f, 1.8f);

        [SerializeField, Min(0f)]
        [Tooltip("Splash around the thrashing bottle. Standing on top of a spinning flamethrower must be the worst place in the room, not the safest.")]
        private float splashRadius = 3f;

        [Header("Burning ground")]
        [SerializeField, Min(0.3f)] private float patchRadius = 1.5f;
        [SerializeField, Min(0f)] private float patchDps = 12f;
        [SerializeField, Min(0.5f)] private float patchLifetime = 4f;
        [SerializeField, Min(1)] private int patchesPerRotation = 8;

        [Header("Look")]
        [SerializeField]
        [Tooltip("Transform to tip onto its side. Empty = the boss itself tips, colliders and all.")]
        private Transform visual;

        [SerializeField]
        [Tooltip("Sink the boss as it lies over so the bottle rests on the floor instead of hovering.")]
        private bool settleToGround = true;

        private AoeTelegraph _telegraph;
        private GameObject _jet;
        private Rigidbody _held;
        private bool _heldWasKinematic;
        private Renderer[] _renderers;

        public override float WeightAt(float distance, BossContext ctx)
        {
            return distance <= range ? 4f : 2f;
        }

        protected override IEnumerator Execute(BossContext ctx)
        {
            float dps = damagePerSecond;
            float duration = spinDuration * ctx.FlameDurationMultiplier;
            float yaw = ctx.Boss.eulerAngles.y;

            _renderers = ctx.Boss.GetComponentsInChildren<Renderer>();
            float low = LowestRenderedY();
            float floorY = float.IsInfinity(low) ? ctx.Boss.position.y : low;

            ShowTelegraph(ctx);
            ctx.Anim?.Anticipate(0.45f);
            yield return Wait(telegraph, ctx);
            HideTelegraph();

            HoldPhysics(ctx);
            yield return Tip(ctx, yaw, floorY, onSide: true);
            ctx.Anim?.Pulse(0.7f);

            _jet = new GameObject("FlameJet");
            ParticleSystem flames = FlameVfx.BuildJet(_jet, range, coneHalfAngle);
            flames.Play();

            float totalDeg = 360f * Mathf.Max(1, rotations);
            float patchEvery = totalDeg / Mathf.Max(1, patchesPerRotation * Mathf.Max(1, rotations));
            float nextPatchDeg = patchEvery;
            const float tick = 0.15f;
            var ticker = new FlameTicker();
            float sweptDeg = 0f;
            int patchIndex = 0;

            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                sweptDeg = totalDeg * (t / duration);
                SetPose(ctx, yaw + sweptDeg, 1f);
                if (settleToGround) RestOnFloor(ctx, floorY);

                Vector3 forward = Quaternion.Euler(0f, yaw + sweptDeg, 0f) * Vector3.forward;
                _jet.transform.SetPositionAndRotation(Nozzle(ctx, forward, floorY), Quaternion.LookRotation(forward));

                bool burning = ctx.Player != null &&
                               FlameDamage.InFlame(ctx.Boss.position, forward, coneHalfAngle, range, splashRadius, ctx.Player.position);
                float due = ticker.Tick(burning, Time.deltaTime, tick);
                if (due > 0f && ctx.Player != null) ctx.DamagePlayer(dps * due, ctx.Player.position);

                if (sweptDeg >= nextPatchDeg && patchDps > 0f)
                {
                    nextPatchDeg += patchEvery;

                    float reach = range * ((patchIndex++ % 2 == 0) ? 0.3f : 0.75f);
                    Vector3 spot = ctx.Boss.position + forward * reach;
                    spot.y = floorY;
                    BurningGround.Spawn(spot, patchRadius, ctx.ScaleDamage(patchDps),
                                        patchLifetime * ctx.FlameDurationMultiplier, ctx.Instigator);
                }

                yield return null;
            }

            float last = ticker.Flush();
            if (last > 0f && ctx.Player != null) ctx.DamagePlayer(dps * last, ctx.Player.position);

            DestroyJet();

            ctx.Anim?.Anticipate(0.4f);
            yield return Tip(ctx, yaw + sweptDeg, floorY, onSide: false);
            ReleasePhysics();
            ctx.Anim?.Pulse(0.5f);
            ctx.Anim?.Relax();
        }

        private IEnumerator Tip(BossContext ctx, float yaw, float floorY, bool onSide)
        {
            float time = (onSide ? tipOverTime : uprightTime) / Mathf.Max(0.1f, ctx.SpeedMultiplier);
            for (float t = 0f; t < time; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / time);
                SetPose(ctx, yaw, onSide ? k : 1f - k);
                if (settleToGround) RestOnFloor(ctx, floorY);
                yield return null;
            }
            SetPose(ctx, yaw, onSide ? 1f : 0f);
            if (settleToGround) RestOnFloor(ctx, floorY);
        }

        private void SetPose(BossContext ctx, float yaw, float tip)
        {
            Quaternion spin = Quaternion.Euler(0f, yaw, 0f);
            Quaternion tilt = Quaternion.Euler(90f * Mathf.Clamp01(tip), 0f, 0f);

            if (visual != null)
            {
                ctx.Boss.rotation = spin;
                visual.localRotation = tilt;
            }
            else
            {
                ctx.Boss.rotation = spin * tilt;
            }
        }

        private Vector3 Nozzle(BossContext ctx, Vector3 forward, float floorY)
        {
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 p = ctx.Boss.position + right * nozzleOffset.x + forward * nozzleOffset.z;
            p.y = floorY + nozzleOffset.y;
            return p;
        }

        private float LowestRenderedY()
        {
            float minY = float.PositiveInfinity;
            if (_renderers != null)
                foreach (Renderer r in _renderers)
                    if (r != null && !(r is ParticleSystemRenderer)) minY = Mathf.Min(minY, r.bounds.min.y);
            return minY;
        }

        private void RestOnFloor(BossContext ctx, float floorY)
        {
            float minY = LowestRenderedY();
            if (float.IsInfinity(minY)) return;
            Vector3 p = ctx.Boss.position;
            p.y += floorY - minY;
            ctx.Boss.position = p;
        }

        private void HoldPhysics(BossContext ctx)
        {
            _held = ctx.Boss.GetComponent<Rigidbody>();
            if (_held == null) return;
            _heldWasKinematic = _held.isKinematic;
            if (!_heldWasKinematic)
            {
                _held.linearVelocity = Vector3.zero;
                _held.angularVelocity = Vector3.zero;
            }
            _held.isKinematic = true;
        }

        private void ReleasePhysics()
        {
            if (_held == null) return;
            _held.isKinematic = _heldWasKinematic;
            _held = null;
        }

        private void DestroyJet()
        {
            if (_jet == null) return;
            Destroy(_jet);
            _jet = null;
        }

        private void ShowTelegraph(BossContext ctx)
        {
            if (_telegraph == null) _telegraph = AoeTelegraph.Create(null);
            _telegraph.Show(ctx.Boss.position, new AoeTelegraphSettings
            {
                Radius = range,
                Color = telegraphColor,
                ScaleMultiplier = 1f,
                PulseSpeed = 2f,
                WarningDuration = telegraph / Mathf.Max(0.1f, ctx.SpeedMultiplier)
            });
        }

        private void HideTelegraph()
        {
            if (_telegraph != null) _telegraph.Hide();
        }

        private void OnDisable() { HideTelegraph(); ReleasePhysics(); DestroyJet(); }
        private void OnDestroy() { ReleasePhysics(); DestroyJet(); if (_telegraph != null) Destroy(_telegraph.gameObject); }
    }
}
