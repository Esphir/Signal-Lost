using System;
using UnityEngine;
using Signal.Combat;
using Signal.Combat.Detection;
using Signal.Combat.Projectiles;
using Signal.Combat.Telegraphs;

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

    private Rigidbody _rb;
    private OverlapSphereHitDetector _detector;
    private readonly CombatHitResolver _resolver = new CombatHitResolver();
    private Action<LobProjectile> _despawnHandler;
    private AoeTelegraph _indicator;

    private bool _launched;
    private bool _exploded;
    private bool _despawned;
    private float _launchTime;
    private float _despawnAt;

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

    /// <summary>Set by ProjectilePool so despawning releases instead of destroying. Optional.</summary>
    public void SetDespawnHandler(Action<LobProjectile> handler) => _despawnHandler = handler;

    /// <summary>
    /// Teleports the projectile to a spawn pose, fully clearing the previous flight's physics
    /// state. Pooled Rigidbodies keep their last pose, momentum, and interpolation history; setting
    /// only <c>transform.position</c> leaves that history intact, so the first rendered frames smear
    /// from the previous landing point (often inside the floor). Teleporting through the Rigidbody
    /// and dropping the interpolation buffer guarantees the shot always starts exactly here.
    /// </summary>
    public void PlaceAt(Vector3 position, Quaternion rotation)
    {
        if (_rb == null) _rb = GetComponent<Rigidbody>();

        _rb.linearVelocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;

        RigidbodyInterpolation previous = _rb.interpolation;
        _rb.interpolation = RigidbodyInterpolation.None; // drops the stale interpolation buffer
        _rb.position = position;
        _rb.rotation = rotation;
        transform.SetPositionAndRotation(position, rotation);
        _rb.interpolation = previous;

        _launched = false;
        _exploded = false;
        _despawned = false;
    }

    /// <summary>Launches with the given velocity and resets all per-flight state (pool-safe). No landing indicator.</summary>
    public void Launch(Vector3 velocity) => Launch(velocity, default, -1f);

    /// <summary>
    /// Launch with the predicted landing point and flight time from the shooter's OWN ballistic
    /// solution — the landing indicator is placed there, so it always matches the real trajectory.
    /// </summary>
    public void Launch(Vector3 velocity, Vector3 predictedLanding, float flightTime)
    {
        _launched = true;
        _exploded = false;
        _despawned = false;
        _launchTime = Time.time;
        _despawnAt = Time.time + config.lifetime;
        _rb.linearVelocity = velocity;
        _rb.angularVelocity = Vector3.zero;

        ShowLandingIndicator(predictedLanding, flightTime);
    }

    private void ShowLandingIndicator(Vector3 predictedLanding, float flightTime)
    {
        if (flightTime <= 0f || !config.showLandingIndicator) return;

        // One telegraph per projectile, created once and reused across pool cycles.
        if (_indicator == null)
            _indicator = AoeTelegraph.Create(config.landingIndicatorPrefab);

        _indicator.Show(predictedLanding, new AoeTelegraphSettings
        {
            Radius = config.explosionRadius,
            Color = config.indicatorColor,
            ScaleMultiplier = config.indicatorScaleMultiplier,
            PulseSpeed = config.indicatorPulseSpeed,
            WarningDuration = flightTime
        });
    }

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

    private void OnCollisionEnter(Collision collision)
    {
        if (_exploded || !_launched) return;
        // Ignore contacts in the first 100ms to prevent self-collision on spawn.
        if (Time.time - _launchTime < 0.1f) return;

        Explode(collision.GetContact(0).point);
    }

    /// <summary>
    /// Raised at the detonation point, once per flight. Audio/VFX listen; this projectile stays
    /// unaware of both.
    /// </summary>
    public event Action<Vector3> Exploded;

    private void Explode(Vector3 point)
    {
        _exploded = true; // hard guard: one explosion per flight
        Exploded?.Invoke(point);

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

        // Clear momentum before returning to the pool so a reused instance can't carry the last
        // flight's velocity into its next spawn (PlaceAt resets again on reuse — belt and braces).
        _rb.linearVelocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;

        // Covers every end-of-flight path: explosion (incl. early terrain hits), mid-air timeout,
        // and pool release — the indicator can never outlive its projectile's flight.
        if (_indicator != null) _indicator.Hide();

        if (_despawnHandler != null) _despawnHandler(this);
        else Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (_indicator != null) Destroy(_indicator.gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        if (config == null) return;
        Gizmos.color = new Color(1f, 0.3f, 0f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, config.explosionRadius);
    }
}
