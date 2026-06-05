using UnityEngine;

/// <summary>
/// AI turret that detects targets within range and fires lobbing projectiles at them.
/// Predicts target movement to lead shots correctly.
/// Attach to a turret GameObject. Assign barrelTip, projectilePrefab, and optionally
/// a turretHead (the part that rotates horizontally) and barrelPivot (pitches up/down).
/// </summary>
public class LobTurret : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The point from which projectiles are spawned.")]
    public Transform barrelTip;

    [Tooltip("The horizontal rotating part of the turret (yaw).")]
    public Transform turretHead;

    [Tooltip("The vertical pitching part of the turret (pitch). Can be null.")]
    public Transform barrelPivot;

    [Tooltip("Projectile prefab to spawn.")]
    public GameObject projectilePrefab;

    [Header("Detection")]
    [Tooltip("Tag used to find targets.")]
    public string targetTag = "Player";

    [Tooltip("Radius in which the turret searches for targets.")]
    public float detectionRange = 20f;

    [Tooltip("Layers the turret can see through when checking line of sight.")]
    public LayerMask obstructionMask;

    [Header("Firing")]
    [Tooltip("Seconds between each shot.")]
    public float fireRate = 2f;

    [Tooltip("Degrees per second the turret head rotates toward the target.")]
    public float rotationSpeed = 90f;

    [Tooltip("How close to facing the predicted target position (degrees) before firing.")]
    public float aimTolerance = 5f;

    [Header("Lob Settings")]
    [Tooltip("Launch angle in degrees above horizontal. Higher = steeper arc.")]
    [Range(10f, 80f)]
    public float lobAngle = 45f;

    [Header("Lead Prediction")]
    [Tooltip("How many iterations to refine the predicted intercept point. 3-5 is usually enough.")]
    [Range(1, 8)]
    public int predictionIterations = 4;

    [Tooltip("Draw a gizmo sphere at the predicted intercept point.")]
    public bool showPredictionGizmo = true;

    // ── Private state ──────────────────────────────────────────────────────
    private Transform _target;
    private Rigidbody _targetRb;          // cached for velocity sampling
    private Vector3 _targetVelocity;      // smoothed velocity estimate
    private Vector3 _lastTargetPos;
    private float _fireTimer;
    private Vector3 _predictedAimPoint;   // stored for gizmos & aiming

    // ──────────────────────────────────────────────────────────────────────

    private void Update()
    {
        FindTarget();

        if (_target == null) return;

        SampleTargetVelocity();

        _predictedAimPoint = PredictInterceptPoint(
            barrelTip.position, _target.position, _targetVelocity, lobAngle, predictionIterations);

        RotateTowardPoint(_predictedAimPoint);

        _fireTimer -= Time.deltaTime;
        if (_fireTimer <= 0f && IsAimedAt(_predictedAimPoint) && HasLineOfSight())
        {
            Fire(_predictedAimPoint);
            _fireTimer = fireRate;
        }
    }

    // ── Target detection ──────────────────────────────────────────────────

    private void FindTarget()
    {
        Transform previous = _target;
        _target = null;
        float closest = detectionRange;

        foreach (Collider col in Physics.OverlapSphere(transform.position, detectionRange))
        {
            if (!col.CompareTag(targetTag)) continue;

            float dist = Vector3.Distance(transform.position, col.transform.position);
            if (dist < closest)
            {
                closest = dist;
                _target = col.transform;
            }
        }

        // Re-cache Rigidbody if target changed
        if (_target != previous)
        {
            _targetRb = _target != null ? _target.GetComponent<Rigidbody>() : null;
            _targetVelocity = Vector3.zero;
            _lastTargetPos = _target != null ? _target.position : Vector3.zero;
        }
    }

    private bool HasLineOfSight()
    {
        if (_target == null) return false;
        Vector3 dir = _target.position - barrelTip.position;
        return !Physics.Raycast(barrelTip.position, dir.normalized, dir.magnitude, obstructionMask);
    }

    // ── Velocity sampling ─────────────────────────────────────────────────

    private void SampleTargetVelocity()
    {
        if (_target == null) return;

        Vector3 rawVelocity;

        if (_targetRb != null)
        {
            // Prefer Rigidbody velocity — accurate and immediate
            rawVelocity = _targetRb.linearVelocity;
        }
        else
        {
            // Finite-difference fallback for non-physics targets (CharacterController, NavMesh, etc.)
            rawVelocity = (_target.position - _lastTargetPos) / Time.deltaTime;
            _lastTargetPos = _target.position;
        }

        // Exponential smoothing to reduce jitter from abrupt direction changes
        _targetVelocity = Vector3.Lerp(_targetVelocity, rawVelocity, 10f * Time.deltaTime);
    }

    // ── Lead prediction ───────────────────────────────────────────────────

    /// <summary>
    /// Iteratively refines an intercept point for a lobbing projectile against a moving target.
    /// Starts with the current target position, calculates flight time, advances the target
    /// by that time, then recalculates — converges in a few iterations.
    /// </summary>
    public static Vector3 PredictInterceptPoint(
        Vector3 origin, Vector3 targetPos, Vector3 targetVelocity,
        float angleDeg, int iterations)
    {
        Vector3 intercept = targetPos;

        for (int i = 0; i < iterations; i++)
        {
            Vector3? vel = CalculateLobVelocity(origin, intercept, angleDeg);
            if (vel == null) break; // unreachable — fall back to current best guess

            float flightTime = EstimateFlightTime(vel.Value, origin, intercept);
            intercept = targetPos + targetVelocity * flightTime;
        }

        return intercept;
    }

    /// <summary>
    /// Estimates ballistic flight time given a launch velocity and known start/end points.
    /// Uses the horizontal component (unaffected by gravity) for accuracy.
    /// </summary>
    private static float EstimateFlightTime(Vector3 launchVelocity, Vector3 origin, Vector3 target)
    {
        float horizontalSpeed = new Vector3(launchVelocity.x, 0f, launchVelocity.z).magnitude;
        float horizontalDist = new Vector3(target.x - origin.x, 0f, target.z - origin.z).magnitude;

        if (horizontalSpeed < 0.001f) return 0f;
        return horizontalDist / horizontalSpeed;
    }

    // ── Aiming ────────────────────────────────────────────────────────────

    private void RotateTowardPoint(Vector3 point)
    {
        Transform yawPivot = turretHead != null ? turretHead : transform;
        Vector3 flatDir = point - yawPivot.position;
        flatDir.y = 0f;

        if (flatDir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(flatDir);
            yawPivot.rotation = Quaternion.RotateTowards(
                yawPivot.rotation, targetRot, rotationSpeed * Time.deltaTime);
        }

        if (barrelPivot != null)
        {
            Vector3 aimEuler = barrelPivot.localEulerAngles;
            float targetPitch = -lobAngle;
            aimEuler.x = Mathf.MoveTowardsAngle(aimEuler.x, targetPitch, rotationSpeed * Time.deltaTime);
            barrelPivot.localEulerAngles = aimEuler;
        }
    }

    private bool IsAimedAt(Vector3 point)
    {
        Transform yawPivot = turretHead != null ? turretHead : transform;
        Vector3 flatDir = point - yawPivot.position;
        flatDir.y = 0f;
        float angle = Vector3.Angle(yawPivot.forward, flatDir);
        return angle <= aimTolerance;
    }

    // ── Firing ────────────────────────────────────────────────────────────

    private void Fire(Vector3 aimPoint)
    {
        if (projectilePrefab == null || barrelTip == null) return;

        Vector3? velocity = CalculateLobVelocity(barrelTip.position, aimPoint, lobAngle);
        if (velocity == null) return;

        GameObject proj = Instantiate(projectilePrefab, barrelTip.position, Quaternion.identity);

        // Prevent the projectile from immediately colliding with the turret itself
        Collider projCol = proj.GetComponent<Collider>();
        if (projCol != null)
        {
            foreach (Collider turretCol in GetComponentsInChildren<Collider>())
                Physics.IgnoreCollision(projCol, turretCol);
        }

        Rigidbody rb = proj.GetComponent<Rigidbody>();
        if (rb != null) rb.linearVelocity = velocity.Value;
    }

    /// <summary>
    /// Calculates the launch velocity for a ballistic arc at a fixed angle.
    /// Returns null if the target is unreachable at the given angle.
    /// </summary>
    public static Vector3? CalculateLobVelocity(Vector3 origin, Vector3 target, float angleDeg)
    {
        float g = Mathf.Abs(Physics.gravity.y);
        float angleRad = angleDeg * Mathf.Deg2Rad;

        Vector3 toTarget = target - origin;
        float dx = new Vector3(toTarget.x, 0f, toTarget.z).magnitude;
        float dy = toTarget.y;

        float tanA = Mathf.Tan(angleRad);
        float cosA = Mathf.Cos(angleRad);
        float denom = 2f * cosA * cosA * (dx * tanA - dy);

        if (denom <= 0f) return null;

        float v = Mathf.Sqrt(g * dx * dx / denom);
        if (float.IsNaN(v) || float.IsInfinity(v)) return null;

        Vector3 horizontalDir = new Vector3(toTarget.x, 0f, toTarget.z).normalized;
        return horizontalDir * v * cosA + Vector3.up * v * Mathf.Sin(angleRad);
    }

    // ── Gizmos ────────────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        if (_target == null || barrelTip == null) return;

        // Current target position
        Gizmos.color = Color.red;
        Gizmos.DrawLine(barrelTip.position, _target.position);

        // Predicted intercept point
        if (showPredictionGizmo && _predictedAimPoint != Vector3.zero)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(barrelTip.position, _predictedAimPoint);
            Gizmos.DrawWireSphere(_predictedAimPoint, 0.3f);
        }
    }
}