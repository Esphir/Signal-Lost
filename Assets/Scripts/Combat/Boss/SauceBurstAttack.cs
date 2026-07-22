// Attack 3 — Sauce Burst, the boss's answer to being hugged.
using System.Collections;
using Signal.Combat.Telegraphs;
using UnityEngine;

namespace Signal.Combat.Boss
{
    public sealed class SauceBurstAttack : BossAttack
    {
        [Header("Telegraph")]
        [SerializeField, Min(0.2f)] private float telegraph = 0.75f;
        [SerializeField] private Color telegraphColor = new Color(1f, 0.25f, 0.15f, 1f);

        [Header("Burst")]
        [SerializeField, Min(1f)] private float radius = 4.5f;
        [SerializeField, Min(1f)] private float damage = 26f;

        [SerializeField, Min(0f)]
        [Tooltip("Beat after the burst before the boss is done — its own recovery, on top of the AI's.")]
        private float settle = 0.35f;

        [SerializeField, Min(0f)]
        [Tooltip("Seconds before it can burst again, so melee range stays dangerous without becoming a lock.")]
        private float repeatDelay = 5f;

        [Header("Burning ground")]
        [SerializeField, Min(1)] private int patchCount = 5;
        [SerializeField, Min(0.3f)] private float patchRadius = 1.4f;
        [SerializeField, Min(0f)] private float patchDps = 10f;
        [SerializeField, Min(0.5f)] private float patchLifetime = 2.5f;

        private AoeTelegraph _telegraph;
        private float _nextBurstAt;

        public override bool CanUse(BossContext ctx) => base.CanUse(ctx) && Time.time >= _nextBurstAt;

        public override float WeightAt(float distance, BossContext ctx)
        {
            return distance <= radius * 1.1f ? 9f : 0f;
        }

        protected override IEnumerator Execute(BossContext ctx)
        {
            ShowTelegraph(ctx);
            ctx.Anim?.Anticipate(0.7f);
            yield return Wait(telegraph, ctx);
            HideTelegraph();

            Burst(ctx);

            ctx.Anim?.Anticipate(-0.35f);
            ctx.Anim?.Pulse(0.5f);
            yield return Wait(settle, ctx);
            ctx.Anim?.Relax();
        }

        private void Burst(BossContext ctx)
        {
            _nextBurstAt = Time.time + repeatDelay;

            if (ctx.Player != null && FlameDamage.InRadius(ctx.Boss.position, radius, ctx.Player.position))
                ctx.DamagePlayer(damage, ctx.Player.position);

            if (patchDps <= 0f) return;

            float baseAngle = Random.value * 360f;
            for (int i = 0; i < patchCount; i++)
            {
                float angle = (baseAngle + i * (360f / patchCount)) * Mathf.Deg2Rad;
                Vector3 spot = ctx.Boss.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (radius * 0.55f);
                spot.y = ctx.Boss.position.y;
                BurningGround.Spawn(spot, patchRadius, ctx.ScaleDamage(patchDps),
                                    patchLifetime * ctx.FlameDurationMultiplier, ctx.Instigator);
            }
        }

        private void ShowTelegraph(BossContext ctx)
        {
            if (_telegraph == null) _telegraph = AoeTelegraph.Create(null);
            _telegraph.Show(ctx.Boss.position, new AoeTelegraphSettings
            {
                Radius = radius,
                Color = telegraphColor,
                ScaleMultiplier = 1f,
                PulseSpeed = 4f,
                WarningDuration = telegraph / Mathf.Max(0.1f, ctx.SpeedMultiplier)
            });
        }

        private void HideTelegraph()
        {
            if (_telegraph != null) _telegraph.Hide();
        }

        private void OnDisable() => HideTelegraph();
        private void OnDestroy() { if (_telegraph != null) Destroy(_telegraph.gameObject); }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.2f, 0.1f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
