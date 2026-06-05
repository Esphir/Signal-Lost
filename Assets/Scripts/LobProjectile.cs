using UnityEngine;

/// <summary>
/// A physics-driven lobbing projectile.
/// Attach to a Rigidbody GameObject used as the projectile prefab.
/// Call Launch(velocity) after instantiation, or let LobTurret do it automatically.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class LobProjectile : MonoBehaviour
{
    [Header("Damage")]
    public float damage = 25f;
    public float splashRadius = 3f;         // 0 = no splash
    public LayerMask damageMask;            // What layers receive damage

    [Header("Impact")]
    [Tooltip("Particle effect spawned on impact. Optional.")]
    public GameObject impactVFX;

    [Tooltip("Seconds before self-destructing if it never hits anything.")]
    public float lifetime = 10f;

    [Header("Trail")]
    [Tooltip("Orient the projectile along its velocity each frame.")]
    public bool orientToVelocity = true;

    // ── Private ───────────────────────────────────────────────────────────
    private Rigidbody _rb;
    private bool _hasLaunched;
    private bool _hasHit;
    private float _spawnTime;

    // ──────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.useGravity = true;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
        _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        _spawnTime = Time.time;
        Destroy(gameObject, lifetime);
    }

    private void FixedUpdate()
    {
        if (!_hasLaunched || _hasHit) return;

        if (orientToVelocity && _rb.linearVelocity.sqrMagnitude > 0.1f)
            transform.rotation = Quaternion.LookRotation(_rb.linearVelocity);
    }

    // ── Public API ────────────────────────────────────────────────────────

    /// <summary>Sets the Rigidbody velocity to launch the projectile.</summary>
    public void Launch(Vector3 velocity)
    {
        _rb.linearVelocity = velocity;
        _hasLaunched = true;
    }

    // ── Collision ─────────────────────────────────────────────────────────

    private void OnCollisionEnter(Collision collision)
    {
        if (_hasHit) return;
        // Ignore collisions in the first 100ms to prevent self-collision on spawn
        if (Time.time - _spawnTime < 0.1f) return;
        _hasHit = true;

        // Splash damage
        if (splashRadius > 0f)
        {
            foreach (Collider col in Physics.OverlapSphere(transform.position, splashRadius, damageMask))
            {
                // Look for any component with a TakeDamage method via interface or SendMessage.
                col.SendMessage("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);
            }
        }
        else
        {
            // Direct hit only
            collision.collider.SendMessage("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);
        }

        // Spawn VFX
        if (impactVFX != null)
            Instantiate(impactVFX, transform.position, Quaternion.identity);

        Destroy(gameObject);
    }

    // ── Gizmos ────────────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        if (splashRadius > 0f)
        {
            Gizmos.color = new Color(1f, 0.3f, 0f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, splashRadius);
        }
    }
}