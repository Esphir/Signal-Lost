using UnityEngine;

/// <summary>
/// Soft lock-on system. Reads input from PlayerInputHandler.
/// Notifies PlayerFollowCamera to frame the locked target.
/// </summary>
public class PlayerLockOn : MonoBehaviour
{
    [Header("Detection")]
    public string enemyTag          = "Enemy";
    public float  lockOnRange       = 15f;
    public float  lockOnFOV         = 60f;

    [Header("Lock-On UI")]
    public GameObject lockOnIndicator;
    public float targetHeightOffset = 1f;

    [Header("Cycle")]
    public float cycleSensitivity   = 0.5f;

    public bool      HasTarget      => _target != null;
    public Vector3   TargetPosition => _target != null
                                       ? _target.position + Vector3.up * targetHeightOffset
                                       : Vector3.zero;
    public Transform CurrentTarget  => _target;

    private PlayerInputHandler _input;
    private PlayerFollowCamera _followCam;
    private Transform          _target;
    private Camera             _cam;
    private float              _cycleDelay;


    private void Awake()
    {
        _input     = GetComponent<PlayerInputHandler>();
        _cam       = Camera.main;
        _followCam = FindFirstObjectByType<PlayerFollowCamera>();
    }

    private void Update()
    {
        if (_input.LockOnPressedThisFrame)
        {
            if (HasTarget) ReleaseLock();
            else           AcquireLock();
        }

        if (HasTarget)
        {
            ValidateLock();
            CycleTarget();
            UpdateIndicator();
            _followCam?.SetLockOnTarget(TargetPosition);
        }
    }


    private void AcquireLock()
    {
        Transform best = FindBestTarget();
        if (best != null) SetTarget(best);
    }

    private void ReleaseLock()
    {
        _target = null;
        _followCam?.ClearLockOn();

        if (lockOnIndicator != null)
        {
            lockOnIndicator.SetActive(false);
            lockOnIndicator.transform.SetParent(transform, false);
        }
    }

    private void SetTarget(Transform t)
    {
        _target = t;
        if (lockOnIndicator != null)
        {
            lockOnIndicator.SetActive(true);
            lockOnIndicator.transform.SetParent(_target, false);
            lockOnIndicator.transform.localPosition = Vector3.up * targetHeightOffset;
        }
    }


    private void ValidateLock()
    {
        if (_target == null) return;
        if (Vector3.Distance(transform.position, _target.position) > lockOnRange * 1.2f)
            ReleaseLock();
    }


    private void CycleTarget()
    {
        _cycleDelay -= Time.deltaTime;
        float h = _input.MoveInput.x;
        if (Mathf.Abs(h) < cycleSensitivity || _cycleDelay > 0f) return;

        _cycleDelay = 0.4f;

        Collider[] nearby    = Physics.OverlapSphere(transform.position, lockOnRange);
        Transform  best      = null;
        float      bestScore = float.MaxValue;

        foreach (Collider col in nearby)
        {
            if (!col.CompareTag(enemyTag) || col.transform == _target) continue;

            Vector3 toEnemy = (col.transform.position - transform.position).normalized;
            float   dot     = Vector3.Dot(_cam.transform.right, toEnemy);

            if (h > 0f && dot < 0.1f)  continue;
            if (h < 0f && dot > -0.1f) continue;

            float score = Vector3.Distance(transform.position, col.transform.position) - dot * 3f;
            if (score < bestScore) { bestScore = score; best = col.transform; }
        }

        if (best != null) { ReleaseLock(); SetTarget(best); }
    }


    private Transform FindBestTarget()
    {
        Collider[] hits      = Physics.OverlapSphere(transform.position, lockOnRange);
        Transform  best      = null;
        float      bestScore = float.MaxValue;

        foreach (Collider col in hits)
        {
            if (!col.CompareTag(enemyTag)) continue;

            Vector3 dir   = col.transform.position - transform.position;
            float   angle = Vector3.Angle(_cam.transform.forward, dir);
            if (angle > lockOnFOV * 0.5f) continue;

            float score = dir.magnitude + angle * 0.5f;
            if (score < bestScore) { bestScore = score; best = col.transform; }
        }

        return best;
    }


    private void UpdateIndicator()
    {
        if (lockOnIndicator == null || _cam == null) return;
        lockOnIndicator.transform.LookAt(lockOnIndicator.transform.position + _cam.transform.forward);
    }


    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, lockOnRange);
        if (_target != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(transform.position, _target.position);
        }
    }
}
