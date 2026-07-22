// Third-person Cinemachine camera rig: look, zoom, framing, lock-on and shoulder offset.
using UnityEngine;
using Unity.Cinemachine;

[DefaultExecutionOrder(-20)]
public class PlayerFollowCamera : MonoBehaviour
{
    [Header("Cinemachine")]
    [Tooltip("Auto-found on this GameObject if left empty.")]
    public CinemachineCamera vcam;

    [Header("Sensitivity")]
    [Tooltip("Mouse look sensitivity, in degrees per unit of mouse movement. Try 0.15–0.4 for comfortable feel.")]
    public float mouseSensitivity = 0.25f;

    [Tooltip("Controller look speed in degrees per second at full stick deflection. Different unit to the mouse on purpose: a stick is a rate, a mouse is a delta. Try 150–300.")]
    public float gamepadSensitivity = 220f;

    [Header("Smoothing")]
    [Tooltip("How quickly the camera catches up to look input. Lower = heavier and calmer, higher = snappier. 8–20 is a good range; drop it if the camera feels dizzying.")]
    public float cameraSmoothing = 12f;

    [Header("Pitch Limits")]
    [Tooltip("Lowest the camera can look (negative = below horizon). -5 prevents ground clipping.")]
    public float minPitch = -5f;
    [Tooltip("Highest the camera can look. 75 stops it flipping over the top of the player.")]
    public float maxPitch = 75f;

    [Header("Distance")]
    [Tooltip("Where the camera starts, and the furthest it may ever sit. Zooming can only pull closer than this, never further out.")]
    public float cameraDistance = 7.725f;

    [Tooltip("Closest the player may zoom in.")]
    public float minRadius = 2f;

    [Tooltip("Distance changed per scroll notch.")]
    public float zoomSpeed = 2f;

    [Header("Lock-On")]
    [Tooltip("How fast the camera swings to frame a locked target.")]
    public float lockOnLerpSpeed = 6f;

    [Header("Framing")]
    [Tooltip("Raises the camera's aim so the player sits lower in frame and more of what's ahead — enemies included — is visible. 0 centres the player.")]
    public float aimHeightOffset = 0.5f;

    [Header("Shoulder")]
    [Tooltip("How far to the side the camera sits. The settings menu picks the sign: right = +, left = -.")]
    public float shoulderOffset = 1f;
    [Tooltip("How fast the camera slides across when the player switches shoulders. 0 = instant snap.")]
    public float shoulderSwitchSpeed = 8f;

    private PlayerInputHandler _input;
    private CinemachineOrbitalFollow _orbital;
    private CinemachineCameraOffset _cameraOffset;
    private CinemachineInputAxisController _axisInput;
    private float _currentShoulder;
    private bool _lockOnActive;
    private Vector3 _lockOnWorldPoint;

    private float _targetYaw;
    private float _targetPitch;

    private void Start()
    {
        if (vcam == null)
            vcam = GetComponent<CinemachineCamera>();

        if (vcam != null)
        {
            _orbital = vcam.GetComponent<CinemachineOrbitalFollow>();
            _axisInput = vcam.GetComponent<CinemachineInputAxisController>();
        }

        if (_orbital == null)
            Debug.LogWarning("PlayerFollowCamera: No CinemachineOrbitalFollow found.");

        if (_axisInput != null) _axisInput.enabled = false;

        if (vcam != null)
        {
            _cameraOffset = vcam.GetComponent<CinemachineCameraOffset>();
            if (_cameraOffset == null) _cameraOffset = vcam.gameObject.AddComponent<CinemachineCameraOffset>();
            _cameraOffset.ApplyAfter = CinemachineCore.Stage.Aim;
        }

        _currentShoulder = shoulderOffset * Signal.UI.SettingsStore.CameraSide;
        ApplyShoulderOffset();

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            _input = player.GetComponent<PlayerInputHandler>();

        if (_orbital != null)
        {
            _orbital.Radius = cameraDistance;
            _targetYaw   = _orbital.HorizontalAxis.Value;
            _targetPitch = _orbital.VerticalAxis.Value;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void LateUpdate()
    {
        UpdateShoulder();

        bool frozen = Signal.UI.UiModalState.AnyOpen || Time.timeScale <= 0f;
        if (frozen || _orbital == null || _input == null) return;

        if (_lockOnActive)
            ApplyLockOnRotation();
        else
            ApplyLookOrbit();

        HandleZoom();
    }

    private void HandleZoom()
    {
        float scroll = _input.ScrollInput;
        if (Mathf.Abs(scroll) < 0.01f) return;

        _orbital.Radius = Mathf.Clamp(
            _orbital.Radius - scroll * zoomSpeed,
            Mathf.Min(minRadius, cameraDistance), cameraDistance);
    }

    private void UpdateShoulder()
    {
        if (_cameraOffset == null) return;

        float target = shoulderOffset * Signal.UI.SettingsStore.CameraSide;
        if (Mathf.Approximately(_currentShoulder, target))
        {
            if (!Mathf.Approximately(_cameraOffset.Offset.y, aimHeightOffset)) ApplyShoulderOffset();
            return;
        }

        if (shoulderSwitchSpeed <= 0f)
        {
            _currentShoulder = target;
        }
        else
        {
            float t = 1f - Mathf.Exp(-shoulderSwitchSpeed * Time.unscaledDeltaTime);
            _currentShoulder = Mathf.Lerp(_currentShoulder, target, t);
            if (Mathf.Abs(target - _currentShoulder) < 0.001f) _currentShoulder = target;
        }

        ApplyShoulderOffset();
    }

    private void ApplyShoulderOffset()
    {
        if (_cameraOffset == null) return;
        Vector3 offset = _cameraOffset.Offset;
        offset.x = _currentShoulder;
        offset.y = aimHeightOffset;
        _cameraOffset.Offset = offset;
    }

    private void ApplyLookOrbit()
    {
        Vector2 look = _input.LookInput;

        float sensitivity = _input.LookIsAnalog
            ? gamepadSensitivity * Time.deltaTime
            : mouseSensitivity;
        sensitivity *= Signal.UI.SettingsStore.MouseSensitivity;

        _targetYaw   += look.x * sensitivity;
        _targetPitch  = Mathf.Clamp(_targetPitch - look.y * sensitivity, minPitch, maxPitch);

        float t = 1f - Mathf.Exp(-cameraSmoothing * Time.deltaTime);
        _orbital.HorizontalAxis.Value = Mathf.LerpAngle(_orbital.HorizontalAxis.Value, _targetYaw, t);
        _orbital.VerticalAxis.Value   = Mathf.Lerp(_orbital.VerticalAxis.Value, _targetPitch, t);
    }

    private void ApplyLockOnRotation()
    {
        if (vcam.Follow == null) return;

        Vector3 dir = _lockOnWorldPoint - vcam.Follow.position;
        if (dir.sqrMagnitude < 0.001f) return;

        Quaternion desired = Quaternion.LookRotation(dir);
        float targetYaw = desired.eulerAngles.y;
        float targetPitch = desired.eulerAngles.x;
        if (targetPitch > 180f) targetPitch -= 360f;

        targetPitch = Mathf.Clamp(targetPitch, minPitch, maxPitch);

        float speed = lockOnLerpSpeed * Time.deltaTime;
        _targetYaw   = Mathf.LerpAngle(_targetYaw, targetYaw, speed);
        _targetPitch = Mathf.LerpAngle(_targetPitch, targetPitch, speed);

        _orbital.HorizontalAxis.Value = _targetYaw;
        _orbital.VerticalAxis.Value   = _targetPitch;
    }

    public void SetLockOnTarget(Vector3 worldPoint)
    {
        _lockOnActive = true;
        _lockOnWorldPoint = worldPoint;
    }

    public void ClearLockOn() => _lockOnActive = false;
}
