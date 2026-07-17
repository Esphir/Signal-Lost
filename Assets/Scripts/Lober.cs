using UnityEngine;
using Signal.Combat.Interfaces;
using Signal.Combat.Projectiles;

/// <summary>
/// AI turret that detects targets within range and fires lobbing projectiles at them.
/// Predicts target movement to lead shots correctly.
/// Attach to a turret GameObject. Assign barrelTip, projectilePrefab, and optionally
/// a turretHead (the part that rotates horizontally) and barrelPivot (pitches up/down).
/// If an IStunnable component (e.g. ImpactStunController) is present, the turret stops
/// tracking and firing while stunned; immune variants simply omit that component.
/// </summary>
public class LobTurret : MonoBehaviour
{
    /// <summary>
    /// Raised the moment a projectile leaves the barrel, with the muzzle position. Lets audio/VFX
    /// react without this turret depending on either of them.
    /// </summary>
    public event System.Action<Vector3> Fired;

    [Header("References")]
    [Tooltip("The point from which projectiles are spawned.")]
    public Transform barrelTip;

    [Tooltip("The horizontal rotating part of the turret (yaw).")]
    public Transform turretHead;

    [Tooltip("Projectile spawn point (and vertical pitch pivot). Falls back to Barrel Tip with a console warning when unassigned.")]
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
    [Tooltip("Normal (minimum) launch angle in degrees, used at and beyond Close Range Distance.")]
    [Range(10f, 80f)]
    public float lobAngle = 45f;

    [Header("Close-Range Arc")]
    [Min(0f)]
    [Tooltip("Below this distance to the target the launch angle steepens, trading flat fast shots for high slow arcs the player can react to. 0 disables the adjustment.")]
    public float closeRangeDistance = 8f;
    [Range(10f, 85f)]
    [Tooltip("Launch angle used at point-blank range. The shot still lands exactly on target — it just hangs longer.")]
    public float maxLaunchAngle = 70f;
    [Min(0.1f)]
    [Tooltip("How aggressively the arc steepens as the player closes in. 1 = linear ramp; 2 = full max angle already at half the close-range distance (even more reaction time).")]
    public float reactionTimeMultiplier = 1f;

    [Header("Lead Prediction")]
    [Range(0f, 1f)]
    [Tooltip("0 = aim at the player's current position (default — most accurate). 1 = fully lead the target by its velocity. Lob flight times are long, so even small values shift shots far ahead.")]
    public float predictionStrength = 0f;

    [Min(0f)]
    [Tooltip("Hard cap on how far ahead of the player the turret may aim, in meters.")]
    public float maxPredictionDistance = 4f;

    [Tooltip("How many iterations to refine the predicted intercept point (only used when Prediction Strength > 0).")]
    [Range(1, 8)]
    public int predictionIterations = 4;

    [Tooltip("Draw a gizmo sphere at the predicted intercept point.")]
    public bool showPredictionGizmo = true;

    private Transform _target;
    private Rigidbody _targetRb;
    private Vector3 _targetVelocity;
    private Vector3 _lastTargetPos;
    private float _fireTimer;
    private Vector3 _predictedAimPoint;
    private IStunnable _stunnable;
    private ProjectilePool _pool;
    private LobProjectile _projectileTemplate;
    private float _projectileGravity;
    private float _currentLaunchAngle;


    /// <summary>Where projectiles spawn: BarrelPivot when assigned, else BarrelTip, else the root.</summary>
    private Transform SpawnPoint =>
        barrelPivot != null ? barrelPivot :
        barrelTip != null ? barrelTip : transform;

    private void Awake()
    {
        _stunnable = GetComponent<IStunnable>();
        _pool = GetComponent<ProjectilePool>();
        _currentLaunchAngle = lobAngle;

        if (barrelPivot == null)
            Debug.LogWarning($"[Combat] LobTurret '{name}': 'Barrel Pivot' is not assigned — projectiles will spawn from {(barrelTip != null ? "Barrel Tip" : "the turret root")} instead.", this);

        _projectileTemplate = projectilePrefab != null ? projectilePrefab.GetComponent<LobProjectile>() : null;
        if (_projectileTemplate == null)
            Debug.LogError("[Combat] LobTurret: projectile prefab has no LobProjectile component.", this);

        // Prediction must use the gravity the projectile will actually experience (its config's
        // gravity scale), or lead shots would miss whenever the scale isn't 1.
        _projectileGravity = _projectileTemplate != null && _projectileTemplate.Config != null
            ? _projectileTemplate.Config.EffectiveGravity
            : Mathf.Abs(Physics.gravity.y);

        if (_pool != null && !_pool.HasPrefab && _projectileTemplate != null)
            _pool.SetPrefab(_projectileTemplate);
    }

    private void Update()
    {
        if (_stunnable != null && _stunnable.IsStunned) return;

        FindTarget();

        if (_target == null) return;

        SampleTargetVelocity();

        // One angle per frame, used consistently by prediction, barrel pitch, and firing —
        // the aim solution and the launched projectile can never disagree.
        _currentLaunchAngle = CurrentLaunchAngle();

        _predictedAimPoint = PredictInterceptPoint(
            SpawnPoint.position, _target.position, _targetVelocity, _currentLaunchAngle, predictionIterations,
            _projectileGravity, predictionStrength, maxPredictionDistance);

        RotateTowardPoint(_predictedAimPoint);

        _fireTimer -= Time.deltaTime;
        if (_fireTimer <= 0f && IsAimedAt(_predictedAimPoint) && HasLineOfSight())
        {
            Fire(_predictedAimPoint);
            _fireTimer = fireRate;
        }
    }


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
        Vector3 dir = _target.position - SpawnPoint.position;
        return !Physics.Raycast(SpawnPoint.position, dir.normalized, dir.magnitude, obstructionMask);
    }


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


    /// <summary>
    /// Aim-point selection with tunable lead. Accuracy first: at strength 0 (default) this is
    /// simply the target's current position. With strength > 0 the classic iterative intercept
    /// runs, but the lead is scaled by <paramref name="predictionStrength"/>, uses only the
    /// target's horizontal velocity (jumps/falls are ignored), is hard-capped at
    /// <paramref name="maxPredictionDistance"/>, and falls back to the current position whenever
    /// the ballistic solution can't reach the predicted point.
    /// </summary>
    public static Vector3 PredictInterceptPoint(
        Vector3 origin, Vector3 targetPos, Vector3 targetVelocity,
        float angleDeg, int iterations, float gravity,
        float predictionStrength, float maxPredictionDistance)
    {
        if (predictionStrength <= 0.001f) return targetPos;

        // Vertical velocity is irrelevant to where the player will be standing.
        Vector3 flatVelocity = new Vector3(targetVelocity.x, 0f, targetVelocity.z);
        if (flatVelocity.sqrMagnitude < 0.04f) return targetPos; // effectively stationary

        Vector3 intercept = targetPos;

        for (int i = 0; i < iterations; i++)
        {
            Vector3? vel = CalculateLobVelocity(origin, intercept, angleDeg, gravity);
            if (vel == null) return targetPos; // unreachable — reliable fallback: current position

            float flightTime = EstimateFlightTime(vel.Value, origin, intercept);
            Vector3 lead = flatVelocity * (flightTime * predictionStrength);
            if (lead.sqrMagnitude > maxPredictionDistance * maxPredictionDistance)
                lead = lead.normalized * maxPredictionDistance;

            intercept = targetPos + lead;
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
            float targetPitch = -_currentLaunchAngle;
            aimEuler.x = Mathf.MoveTowardsAngle(aimEuler.x, targetPitch, rotationSpeed * Time.deltaTime);
            barrelPivot.localEulerAngles = aimEuler;
        }
    }

    /// <summary>
    /// Distance-based arc: at or beyond Close Range Distance this is the normal Lob Angle; inside
    /// it, the angle ramps toward Max Launch Angle (scaled by Reaction Time Multiplier). A steeper
    /// angle means a higher, slower arc — same exact landing point, more time to see it coming.
    /// </summary>
    private float CurrentLaunchAngle()
    {
        if (_target == null || closeRangeDistance <= 0.01f) return lobAngle;

        Vector3 toTarget = _target.position - SpawnPoint.position;
        toTarget.y = 0f;
        float distance = toTarget.magnitude;
        if (distance >= closeRangeDistance) return lobAngle;

        float closeness = Mathf.Clamp01((1f - distance / closeRangeDistance) * reactionTimeMultiplier);
        return Mathf.Lerp(lobAngle, Mathf.Max(lobAngle, maxLaunchAngle), closeness);
    }

    private bool IsAimedAt(Vector3 point)
    {
        Transform yawPivot = turretHead != null ? turretHead : transform;
        Vector3 flatDir = point - yawPivot.position;
        flatDir.y = 0f;
        float angle = Vector3.Angle(yawPivot.forward, flatDir);
        return angle <= aimTolerance;
    }


    private void Fire(Vector3 aimPoint)
    {
        if (_projectileTemplate == null) return;

        // IsAimedAt() has already verified we're facing the target before this is called.
        // Read spawn pose fresh from the barrel every shot so it never lags the pivot's aim.
        Vector3 origin = SpawnPoint.position;
        Quaternion spawnRotation = SpawnPoint.rotation;
        Vector3? velocity = CalculateLobVelocity(origin, aimPoint, _currentLaunchAngle, _projectileGravity);
        if (velocity == null && _target != null)
        {
            // Predicted point unreachable — fall back to the player's current position.
            aimPoint = _target.position;
            velocity = CalculateLobVelocity(origin, aimPoint, _currentLaunchAngle, _projectileGravity);
        }
        if (velocity == null) return;

        // Pooled when a ProjectilePool sits next to this turret; plain instantiate otherwise.
        // Both spawn exactly at the barrel pose; the pool path resets pooled Rigidbody state.
        LobProjectile proj = _pool != null
            ? _pool.Spawn(origin, spawnRotation)
            : Instantiate(_projectileTemplate, origin, spawnRotation);
        if (proj == null) return;

        // Prevent the projectile from immediately colliding with the turret itself
        Collider projCol = proj.GetComponent<Collider>();
        if (projCol != null)
        {
            foreach (Collider turretCol in GetComponentsInChildren<Collider>())
                Physics.IgnoreCollision(projCol, turretCol);
        }

        // The landing point and flight time come from the SAME ballistic solution used to launch,
        // so the projectile's landing indicator matches the real impact without duplicated math.
        float flightTime = EstimateFlightTime(velocity.Value, origin, aimPoint);
        proj.Launch(velocity.Value, aimPoint, flightTime);

        Fired?.Invoke(origin); // notification only — this turret knows nothing about audio or VFX
    }

    /// <summary>
    /// Calculates the launch velocity for a ballistic arc at a fixed angle under the given
    /// gravity magnitude. Returns null if the target is unreachable at the given angle.
    /// </summary>
    public static Vector3? CalculateLobVelocity(Vector3 origin, Vector3 target, float angleDeg, float gravity)
    {
        float g = gravity;
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


    private void OnDrawGizmosSelected()
    {
        // Yellow = attack/detection range.
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        // Orange = explosion radius of the configured projectile (live from its config asset),
        // drawn at the predicted landing point while playing, else around the turret.
        LobProjectile template = _projectileTemplate != null ? _projectileTemplate
            : projectilePrefab != null ? projectilePrefab.GetComponent<LobProjectile>() : null;
        if (template != null && template.Config != null)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f);
            Vector3 explosionCenter = Application.isPlaying && _predictedAimPoint != Vector3.zero
                ? _predictedAimPoint
                : transform.position;
            Gizmos.DrawWireSphere(explosionCenter, template.Config.explosionRadius);
        }

        if (_target == null) return;

        // Red = current target, cyan = predicted intercept.
        Gizmos.color = Color.red;
        Gizmos.DrawLine(SpawnPoint.position, _target.position);

        if (showPredictionGizmo && _predictedAimPoint != Vector3.zero)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(SpawnPoint.position, _predictedAimPoint);
            Gizmos.DrawWireSphere(_predictedAimPoint, 0.3f);
        }
    }
}