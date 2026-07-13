using System;
using UnityEngine;
using Signal.Combat;
using Signal.Combat.Detection;
using Signal.Combat.Projectiles;

/// <summary>
/// A physics-driven explosive projectile. All gameplay data comes from a
/// <see cref="ProjectileConfigSO"/> — this component only executes it.
/// Explodes once on collision (or despawns quietly at end of lifetime), damaging every
/// IDamageable inside the explosion radius via the shared combat resolver.
/// Supports pooling: when a despawn handler is set (by <see cref="ProjectilePool"/>) the
/// projectile is released instead of destroyed.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class LobProjectile : MonoBehaviour
{
    [SerializeField]
    [Tooltip("All projectile data (damage, gravity, explosion, lifetime) lives in this asset.")]
    private ProjectileConfigSO config;

    [SerializeField]
    [Tooltip("Orient the projectile along its velocity each frame.")]
    private bool orientToVelocity = true;

    public ProjectileConfigSO Config => config;

    // ── Private ───────────────────────────────────────────────────────────
    private Rigidbody _rb;
    private OverlapSphereHitDetector _detector;
    private readonly CombatHitResolver _resolver = new CombatHitResolver();
    private Action<LobProjectile> _despawnHandler;

    private bool _launched;
    private bool _exploded;
    private bool _despawned;
    private float _launchTime;
    private float _despawnAt;

    // ──────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
        _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        // Gravity is applied manually so the config's gravity scale works.
        _rb.useGravity = false;

        if (config == null)
        {
            Debug.LogError($"[Combat] LobProjectile '{name}' has no ProjectileConfigSO assigned.", this);
            enabled = false;
            return;
        }

        _detector = new OverlapSphereHitDetector(config.maxExplosionTargets);
    }

    // ── Public API ────────────────────────────────────────────────────────

    /// <summary>Set by ProjectilePool so despawning releases instead of destroying. Optional.</summary>
    public void SetDespawnHandler(Action<LobProjectile> handler) => _despawnHandler = handler;

    /// <summary>Launches with the given velocity and resets all per-flight state (pool-safe).</summary>
    public void Launch(Vector3 velocity)
    {
        _launched = true;
        _exploded = false;
        _despawned = false;
        _launchTime = Time.time;
        _despawnAt = Time.time + config.lifetime;
        _rb.linearVelocity = velocity;
        _rb.angularVelocity = Vector3.zero;
    }

    // ── Flight ────────────────────────────────────────────────────────────

    private void FixedUpdate()
    {
        if (!_launched || _exploded) return;

        _rb.AddForce(Physics.gravity * config.gravityScale, ForceMode.Acceleration);

        if (orientToVelocity && _rb.linearVelocity.sqrMagnitude > 0.1f)
            transform.rotation = Quaternion.LookRotation(_rb.linearVelocity);
    }

    private void Update()
    {
        if (_launched && !_exploded && Time.time >= _despawnAt)
            Despawn(); // timed out mid-air — vanish without exploding
    }

    // ── Explosion ─────────────────────────────────────────────────────────

    private void OnCollisionEnter(Collision collision)
    {
        if (_exploded || !_launched) return;
        // Ignore contacts in the first 100ms to prevent self-collision on spawn.
        if (Time.time - _launchTime < 0.1f) return;

        Explode(collision.GetContact(0).point);
    }

    private void Explode(Vector3 point)
    {
        _exploded = true; // hard guard: one explosion per flight

        int count = _detector.Detect(point, config.explosionRadius, config.damageMask);
        _resolver.BeginSwing();
        int hits = _resolver.ApplyDamage(_detector.Buffer, count, config.damage, gameObject, point);
        CombatLog.Info($"'{name}' exploded: {count} collider(s) in radius {config.explosionRadius:0.#}, {hits} target(s) damaged for {config.damage:0.#}.", this);

        if (config.explosionVfx != null)
            Instantiate(config.explosionVfx, point, Quaternion.identity);
        if (config.explosionSfx != null)
            AudioSource.PlayClipAtPoint(config.explosionSfx, point, config.sfxVolume);

        Despawn();
    }

    private void Despawn()
    {
        if (_despawned) return;
        _despawned = true;
        _launched = false;

        if (_despawnHandler != null) _despawnHandler(this);
        else Destroy(gameObject);
    }

    // ── Gizmos ────────────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        if (config == null) return;
        Gizmos.color = new Color(1f, 0.3f, 0f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, config.explosionRadius);
    }
}
