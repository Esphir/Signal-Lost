// AI turret that detects targets within range and fires lobbing projectiles at them.
using UnityEngine;
using Signal.Combat.Interfaces;
using Signal.Combat.Projectiles;

public class KernelCannon : MonoBehaviour
{
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

    [Header("Aim Scatter")]
    [Min(0f)]
    [Tooltip("The turret aims at a random point within this radius (metres) of the target instead of dead-on, so shots aren't always centred on the player and leave room to dodge-roll clear. A fresh point is rolled for each shot; 0 = always aim exactly at the target.")]
    public float aimScatterRadius = 2.5f;

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
    private Vector3 _aimOffset;
    private IStunnable _stunnable;
    private ProjectilePool _pool;
    private LobProjectile _projectileTemplate;
    private float _projectileGravity;
    private float _currentLaunchAngle;

    private Transform SpawnPoint =>
        barrelPivot != null ? barrelPivot :
        barrelTip != null ? barrelTip : transform;

    private void Awake()
    {
        _stunnable = GetComponent<IStunnable>();
        _pool = GetComponent<ProjectilePool>();
        _currentLaunchAngle = lobAngle;

        if (barrelPivot == null)
            Debug.LogWarning($"[Combat] KernelCannon '{name}': 'Barrel Pivot' is not assigned — projectiles will spawn from {(barrelTip != null ? "Barrel Tip" : "the turret root")} instead.", this);

        _projectileTemplate = projectilePrefab != null ? projectilePrefab.GetComponent<LobProjectile>() : null;
        if (_projectileTemplate == null)
            Debug.LogError("[Combat] KernelCannon: projectile prefab has no LobProjectile component.", this);

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

        _currentLaunchAngle = CurrentLaunchAngle();

        // Offset the aim by a fixed per-shot amount so the shot lands near the player rather than
        // dead-on. It's held steady between shots (not re-rolled per frame) so the head can settle
        // within aimTolerance and actually fire, then re-rolled once the shot goes out.
        _predictedAimPoint = PredictInterceptPoint(
            SpawnPoint.position, _target.position, _targetVelocity, _currentLaunchAngle, predictionIterations,
            _projectileGravity, predictionStrength, maxPredictionDistance) + _aimOffset;

        RotateTowardPoint(_predictedAimPoint);

        _fireTimer -= Time.deltaTime;
        if (_fireTimer <= 0f && IsAimedAt(_predictedAimPoint) && HasLineOfSight())
        {
            Fire(_predictedAimPoint);
            _fireTimer = fireRate;
            RerollAimScatter();
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
            RerollAimScatter();
        }
    }

    // Pick a fresh aim offset: a random point within aimScatterRadius on the ground plane. Called
    // once per shot (and on acquiring a target) so each lob lands somewhere new around the player.
    private void RerollAimScatter()
    {
        if (aimScatterRadius <= 0.001f) { _aimOffset = Vector3.zero; return; }
        Vector2 disc = Random.insideUnitCircle * aimScatterRadius;
        _aimOffset = new Vector3(disc.x, 0f, disc.y);
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
            rawVelocity = _targetRb.linearVelocity;
        }
        else
        {
            rawVelocity = (_target.position - _lastTargetPos) / Time.deltaTime;
            _lastTargetPos = _target.position;
        }

        _targetVelocity = Vector3.Lerp(_targetVelocity, rawVelocity, 10f * Time.deltaTime);
    }

    public static Vector3 PredictInterceptPoint(
        Vector3 origin, Vector3 targetPos, Vector3 targetVelocity,
        float angleDeg, int iterations, float gravity,
        float predictionStrength, float maxPredictionDistance)
    {
        if (predictionStrength <= 0.001f) return targetPos;

        Vector3 flatVelocity = new Vector3(targetVelocity.x, 0f, targetVelocity.z);
        if (flatVelocity.sqrMagnitude < 0.04f) return targetPos;

        Vector3 intercept = targetPos;

        for (int i = 0; i < iterations; i++)
        {
            Vector3? vel = CalculateLobVelocity(origin, intercept, angleDeg, gravity);
            if (vel == null) return targetPos;

            float flightTime = EstimateFlightTime(vel.Value, origin, intercept);
            Vector3 lead = flatVelocity * (flightTime * predictionStrength);
            if (lead.sqrMagnitude > maxPredictionDistance * maxPredictionDistance)
                lead = lead.normalized * maxPredictionDistance;

            intercept = targetPos + lead;
        }

        return intercept;
    }

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

        Vector3 origin = SpawnPoint.position;
        Quaternion spawnRotation = SpawnPoint.rotation;
        Vector3? velocity = CalculateLobVelocity(origin, aimPoint, _currentLaunchAngle, _projectileGravity);
        if (velocity == null && _target != null)
        {
            aimPoint = _target.position;
            velocity = CalculateLobVelocity(origin, aimPoint, _currentLaunchAngle, _projectileGravity);
        }
        if (velocity == null) return;

        LobProjectile proj = _pool != null
            ? _pool.Spawn(origin, spawnRotation)
            : Instantiate(_projectileTemplate, origin, spawnRotation);
        if (proj == null) return;

        Collider projCol = proj.GetComponent<Collider>();
        if (projCol != null)
        {
            foreach (Collider turretCol in GetComponentsInChildren<Collider>())
                Physics.IgnoreCollision(projCol, turretCol);
        }

        float flightTime = EstimateFlightTime(velocity.Value, origin, aimPoint);
        proj.Launch(velocity.Value, aimPoint, flightTime);

        Fired?.Invoke(origin);
    }

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
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

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

        Gizmos.color = Color.red;
        Gizmos.DrawLine(SpawnPoint.position, _target.position);

        if (aimScatterRadius > 0f)
        {
            Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.5f);
            Gizmos.DrawWireSphere(_target.position, aimScatterRadius);
        }

        if (showPredictionGizmo && _predictedAimPoint != Vector3.zero)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(SpawnPoint.position, _predictedAimPoint);
            Gizmos.DrawWireSphere(_predictedAimPoint, 0.3f);
        }
    }
}
