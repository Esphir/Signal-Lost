// A physics-driven explosive projectile.
using System;
using UnityEngine;
using Signal.Combat;
using Signal.Combat.Detection;
using Signal.Combat.Projectiles;
using Signal.Combat.Telegraphs;

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

        _rb.useGravity = false;

        if (config == null)
        {
            Debug.LogError($"[Combat] LobProjectile '{name}' has no ProjectileConfigSO assigned.", this);
            enabled = false;
            return;
        }

        _detector = new OverlapSphereHitDetector(config.maxExplosionTargets);
    }

    public void SetDespawnHandler(Action<LobProjectile> handler) => _despawnHandler = handler;

    public void PlaceAt(Vector3 position, Quaternion rotation)
    {
        if (_rb == null) _rb = GetComponent<Rigidbody>();

        _rb.linearVelocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;

        RigidbodyInterpolation previous = _rb.interpolation;
        _rb.interpolation = RigidbodyInterpolation.None;
        _rb.position = position;
        _rb.rotation = rotation;
        transform.SetPositionAndRotation(position, rotation);
        _rb.interpolation = previous;

        _launched = false;
        _exploded = false;
        _despawned = false;
    }

    public void Launch(Vector3 velocity) => Launch(velocity, default, -1f);

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

        if (_indicator == null)
            _indicator = AoeTelegraph.Create(config.landingIndicatorPrefab);

        _indicator.Show(GroundProbe.Below(predictedLanding), new AoeTelegraphSettings
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
            Despawn();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (_exploded || !_launched) return;

        if (Time.time - _launchTime < 0.1f) return;

        Explode(collision.GetContact(0).point);
    }

    public event Action<Vector3> Exploded;

    private void Explode(Vector3 point)
    {
        _exploded = true;
        Exploded?.Invoke(point);

        int count = _detector.Detect(point, config.explosionRadius, config.damageMask);
        _resolver.BeginSwing();
        int hits = _resolver.ApplyDamage(_detector.Buffer, count, config.damage, gameObject, point);
        CombatLog.Info($"'{name}' exploded: {count} collider(s) in radius {config.explosionRadius:0.#}, {hits} target(s) damaged for {config.damage:0.#}.", this);

        if (config.explosionVfx != null)
            Instantiate(config.explosionVfx, point, Quaternion.identity);

        Despawn();
    }

    private void Despawn()
    {
        if (_despawned) return;
        _despawned = true;
        _launched = false;

        _rb.linearVelocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;

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
