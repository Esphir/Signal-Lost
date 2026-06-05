using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// Drives a Cinemachine OrbitalFollow camera with the new Input System.
/// Clamps vertical pitch so the camera cannot go underground or flip over the player.
///
/// SETUP:
/// 1. GameObject → Cinemachine → Cinemachine Camera
/// 2. Add CinemachineOrbitalFollow to it
/// 3. Add CinemachineRotationComposer to it
/// 4. Set Tracking Target + Look At to your player
/// 5. On CinemachineOrbitalFollow, DISABLE the built-in Input Axis sources
///    (clear the Input field on Horizontal Axis and Vertical Axis) so this
///    script is the only thing driving them.
/// 6. Also add a CinemachineDeoccluder component — set Strategy to
///    "Pull Camera Forward" to handle wall collision automatically.
/// 7. Attach this script to the same CinemachineCamera GameObject.
/// </summary>
public class PlayerFollowCamera : MonoBehaviour
{
    [Header("Cinemachine")]
    [Tooltip("Auto-found on this GameObject if left empty.")]
    public CinemachineCamera vcam;

    [Header("Sensitivity")]
    [Tooltip("Mouse look sensitivity. Try 0.3–0.8 for comfortable feel.")]
    public float mouseSensitivity = 0.5f;

    [Header("Pitch Limits")]
    [Tooltip("Lowest the camera can look (negative = below horizon). -5 prevents ground clipping.")]
    public float minPitch = -5f;
    [Tooltip("Highest the camera can look. 75 stops it flipping over the top of the player.")]
    public float maxPitch = 75f;

    [Header("Zoom")]
    public float zoomSpeed = 2f;
    public float minRadius = 2f;
    public float maxRadius = 10f;

    [Header("Lock-On")]
    [Tooltip("How fast the camera swings to frame a locked target.")]
    public float lockOnLerpSpeed = 6f;

    // ── Private ───────────────────────────────────────────────────────────
    private PlayerInputHandler _input;
    private CinemachineOrbitalFollow _orbital;
    private bool _lockOnActive;
    private Vector3 _lockOnWorldPoint;

    // ──────────────────────────────────────────────────────────────────────

    private void Start()
    {
        if (vcam == null)
            vcam = GetComponent<CinemachineCamera>();

        if (vcam != null)
            _orbital = vcam.GetComponent<CinemachineOrbitalFollow>();

        if (_orbital == null)
            Debug.LogWarning("PlayerFollowCamera: No CinemachineOrbitalFollow found.");

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            _input = player.GetComponent<PlayerInputHandler>();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void LateUpdate()
    {
        if (_orbital == null || _input == null) return;

        if (_lockOnActive)
            ApplyLockOnRotation();
        else
            ApplyMouseOrbit();

        HandleZoom();
    }

    // ── Free orbit ────────────────────────────────────────────────────────

    private void ApplyMouseOrbit()
    {
        Vector2 look = _input.LookInput;
        _orbital.HorizontalAxis.Value += look.x * mouseSensitivity;

        // Clamp BEFORE adding so we never push past the limit — no jitter
        float newPitch = _orbital.VerticalAxis.Value - look.y * mouseSensitivity;
        _orbital.VerticalAxis.Value = Mathf.Clamp(newPitch, minPitch, maxPitch);
    }

    // ── Lock-on orbit ─────────────────────────────────────────────────────

    private void ApplyLockOnRotation()
    {
        if (vcam.Follow == null) return;

        Vector3 dir = _lockOnWorldPoint - vcam.Follow.position;
        if (dir.sqrMagnitude < 0.001f) return;

        Quaternion desired = Quaternion.LookRotation(dir);
        float targetYaw = desired.eulerAngles.y;
        float targetPitch = desired.eulerAngles.x;
        if (targetPitch > 180f) targetPitch -= 360f;

        // Still respect pitch limits even during lock-on
        targetPitch = Mathf.Clamp(targetPitch, minPitch, maxPitch);

        float speed = lockOnLerpSpeed * Time.deltaTime;
        _orbital.HorizontalAxis.Value = Mathf.LerpAngle(_orbital.HorizontalAxis.Value, targetYaw, speed);
        _orbital.VerticalAxis.Value = Mathf.LerpAngle(_orbital.VerticalAxis.Value, targetPitch, speed);
    }



    // ── Zoom ──────────────────────────────────────────────────────────────

    private void HandleZoom()
    {
        float scroll = _input.ScrollInput;
        if (Mathf.Abs(scroll) < 0.01f) return;

        _orbital.Radius = Mathf.Clamp(
            _orbital.Radius - scroll * zoomSpeed,
            minRadius, maxRadius);
    }

    // ── Public API ────────────────────────────────────────────────────────

    public void SetLockOnTarget(Vector3 worldPoint)
    {
        _lockOnActive = true;
        _lockOnWorldPoint = worldPoint;
    }

    public void ClearLockOn() => _lockOnActive = false;
}