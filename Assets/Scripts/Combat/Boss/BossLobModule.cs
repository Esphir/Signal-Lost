// The boss's occasional lobbed fireball: thrown while it prowls, using the lobber's arc, and leaving a patch of burning ground where it lands that flares briefly then fades.
using Signal.Combat.Projectiles;
using UnityEngine;

namespace Signal.Combat.Boss
{
    [DisallowMultipleComponent]
    public sealed class BossLobModule : MonoBehaviour
    {
        [SerializeField, Min(0.5f)]
        [Tooltip("Seconds between lobs while the boss is moving.")]
        private float cooldown = 3.5f;

        [SerializeField, Range(20f, 80f)]
        [Tooltip("Launch angle — steeper hangs longer and is easier to read.")]
        private float launchAngle = 58f;

        [SerializeField, Min(0f)]
        [Tooltip("Aim is offset by up to this far from the player, so a lob is dodgeable rather than dead-on.")]
        private float aimScatter = 2f;

        [SerializeField] private Vector3 muzzleOffset = new Vector3(0f, 1.4f, 0f);

        [Header("Lingering fire")]
        [SerializeField, Min(0.3f)] private float lingerRadius = 2.2f;
        [SerializeField, Min(0f)] private float lingerDps = 12f;
        [SerializeField, Min(0.5f)] private float lingerLifetime = 2.5f;

        private float _nextLobAt;
        private Collider[] _bossColliders;

        public bool Ready => Time.time >= _nextLobAt;

        private void Awake()
        {
            _bossColliders = GetComponentsInChildren<Collider>();
            _nextLobAt = Time.time + cooldown * 0.5f;
        }

        public void TryLob(BossContext ctx, GameObject projectilePrefab)
        {
            if (!Ready || projectilePrefab == null || !ctx.ResolvePlayer()) return;

            var template = projectilePrefab.GetComponent<LobProjectile>();
            if (template == null) return;

            _nextLobAt = Time.time + cooldown / Mathf.Max(0.1f, ctx.SpeedMultiplier);

            Vector3 origin = ctx.Boss.position + muzzleOffset;
            Vector3 aim = ctx.Player.position;
            if (aimScatter > 0.001f)
            {
                Vector2 disc = Random.insideUnitCircle * aimScatter;
                aim += new Vector3(disc.x, 0f, disc.y);
            }

            float gravity = template.Config != null ? template.Config.EffectiveGravity : Mathf.Abs(Physics.gravity.y);
            Vector3? velocity = LobTurret.CalculateLobVelocity(origin, aim, launchAngle, gravity);
            if (velocity == null) return;

            LobProjectile proj = Instantiate(template, origin, Quaternion.identity);

            Collider projCol = proj.GetComponent<Collider>();
            if (projCol != null && _bossColliders != null)
                foreach (Collider c in _bossColliders)
                    if (c != null) Physics.IgnoreCollision(projCol, c, true);

            BossContext captured = ctx;
            proj.Exploded += point => BurningGround.Spawn(point, lingerRadius,
                captured.ScaleDamage(lingerDps),
                lingerLifetime * captured.FlameDurationMultiplier, captured.Instigator);

            float flightTime = HorizontalFlightTime(velocity.Value, origin, aim);
            proj.Launch(velocity.Value, aim, flightTime);
        }

        private static float HorizontalFlightTime(Vector3 velocity, Vector3 origin, Vector3 target)
        {
            float horizontalSpeed = new Vector3(velocity.x, 0f, velocity.z).magnitude;
            float horizontalDist = new Vector3(target.x - origin.x, 0f, target.z - origin.z).magnitude;
            return horizontalSpeed < 0.001f ? 0f : horizontalDist / horizontalSpeed;
        }
    }
}
