// Third-person player controller mimicking Into the Unwell's movement feel.
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Signal.Run;
using Signal.Stats;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 7f;
    public float sprintSpeed = 11f;
    [Tooltip("Time in seconds to reach full speed from rest. Lower = snappier.")]
    public float accelerationTime = 0.12f;
    [Tooltip("Time in seconds to stop from full speed. Lower = snappier.")]
    public float decelerationTime = 0.08f;
    public float turnSpeed = 720f;

    [Header("Jump & Gravity")]
    public float jumpHeight = 2.2f;
    public float fallMultiplier = 2.5f;
    public float lowJumpMultiplier = 2f;
    public float coyoteTime = 0.15f;
    public float jumpBufferTime = 0.12f;
    [Tooltip("Extra jumps allowed while airborne. 1 = double jump.")]
    public int maxAirJumps = 1;
    [Tooltip("Air (double) jump height as a multiple of Jump Height. >1 makes the second jump higher; 1 = same as the first.")]
    public float airJumpHeightMultiplier = 1.25f;
    [Tooltip("After jumping, ignore the ground check for this long so the player clears it — stops coyote time/air jumps from refreshing and the second jump being treated as a ground jump.")]
    public float jumpGroundIgnoreTime = 0.1f;

    [Header("Ground Check")]
    public float groundCheckRadius = 0.3f;
    public float groundCheckOffset = 0.05f;
    public LayerMask groundMask;

    [Header("Camera")]
    public Transform cameraTransform;

    public bool IsGrounded { get; private set; }
    public bool IsMoving { get; private set; }
    public bool IsSprinting { get; private set; }
    public bool IsRolling => _dodge != null && _dodge.IsRolling;
    public float CurrentSpeed { get; private set; }
    public Vector3 Velocity => _velocity;

    public event Action Jumped;

    public event Action DoubleJumped;

    public event Action<float> Landed;

    private CharacterController _cc;
    private PlayerDodge _dodge;
    private PlayerLockOn _lockOn;
    private PlayerInputHandler _input;
    private Animator _animator;

    private static readonly int HashSpeed = Animator.StringToHash("Speed");

    private Vector3 _velocity;
    private Vector3 _moveVelocity;
    private Vector3 _moveVelocityRef;
    private float _coyoteTimer;
    private float _jumpBufferTimer;
    private int _airJumpsRemaining;
    private float _jumpGraceTimer;
    private bool _wasGrounded;

    private void Awake()
    {
        _cc = GetComponent<CharacterController>();
        _dodge = GetComponent<PlayerDodge>();
        _lockOn = GetComponent<PlayerLockOn>();
        _input = GetComponent<PlayerInputHandler>();

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        _animator = GetComponentInChildren<Animator>();
    }

    private void Update()
    {
        CheckGround();
        HandleCoyoteTime();
        HandleJumpBuffer();
        HandleMovement();
        HandleJump();
        ApplyGravity();
        ApplyMove();
        HandleRotation();
    }

    private void CheckGround()
    {
        if (_jumpGraceTimer > 0f)
        {
            _jumpGraceTimer -= Time.deltaTime;
            IsGrounded = false;
            _wasGrounded = false;
            return;
        }

        Vector3 origin = transform.position + Vector3.up * groundCheckOffset;
        bool grounded = Physics.CheckSphere(origin, groundCheckRadius, groundMask,
                                            QueryTriggerInteraction.Ignore);

        if (grounded && !_wasGrounded && _velocity.y < -0.1f)
            Landed?.Invoke(-_velocity.y);

        _wasGrounded = grounded;
        IsGrounded = grounded;
        if (IsGrounded && _velocity.y < 0f)
            _velocity.y = -2f;
    }

    private void HandleCoyoteTime()
    {
        if (IsGrounded)
        {
            _coyoteTimer = coyoteTime;
            _airJumpsRemaining = maxAirJumps;
        }
        else
        {
            _coyoteTimer -= Time.deltaTime;
        }
    }

    private void HandleJumpBuffer()
    {
        if (_input.JumpPressedThisFrame) _jumpBufferTimer = jumpBufferTime;
        else _jumpBufferTimer -= Time.deltaTime;
    }

    private void HandleMovement()
    {
        if (IsRolling) return;

        Vector2 move = _input.MoveInput;
        IsSprinting = _input.SprintHeld && move.magnitude > 0.1f;

        Vector3 input = new Vector3(move.x, 0f, move.y);
        if (input.sqrMagnitude > 1f) input.Normalize();

        float camYaw = cameraTransform.eulerAngles.y;
        Vector3 camForward = Quaternion.Euler(0f, camYaw, 0f) * Vector3.forward;
        Vector3 camRight = Quaternion.Euler(0f, camYaw, 0f) * Vector3.right;
        Vector3 wishDir = camForward * input.z + camRight * input.x;

        float targetSpeed = RunManager.QueryStat(StatType.MoveSpeed, IsSprinting ? sprintSpeed : moveSpeed);
        float smoothTime = wishDir.sqrMagnitude > 0f ? accelerationTime : decelerationTime;

        _moveVelocity = Vector3.SmoothDamp(_moveVelocity, wishDir * targetSpeed, ref _moveVelocityRef, smoothTime);

        IsMoving = _moveVelocity.sqrMagnitude > 0.01f;
        CurrentSpeed = _moveVelocity.magnitude;

        if (_animator != null)
            _animator.SetFloat(HashSpeed, CurrentSpeed / RunManager.QueryStat(StatType.MoveSpeed, moveSpeed));
    }

    private void HandleJump()
    {
        if (_jumpBufferTimer <= 0f || IsRolling) return;

        bool groundJump = _coyoteTimer > 0f;
        if (!groundJump)
        {
            if (_airJumpsRemaining <= 0) return;
            _airJumpsRemaining--;
        }

        float height = groundJump ? jumpHeight : jumpHeight * airJumpHeightMultiplier;
        _velocity.y = Mathf.Sqrt(height * 2f * Mathf.Abs(Physics.gravity.y));
        _jumpBufferTimer = 0f;
        _coyoteTimer = 0f;
        _jumpGraceTimer = jumpGroundIgnoreTime;

        if (groundJump) Jumped?.Invoke();
        else DoubleJumped?.Invoke();
    }

    private void ApplyGravity()
    {
        if (IsGrounded && _velocity.y <= 0f) return;

        float multiplier = 1f;
        if (_velocity.y < 0f) multiplier = fallMultiplier;
        else if (!_input.JumpHeld) multiplier = lowJumpMultiplier;

        _velocity.y += Physics.gravity.y * multiplier * Time.deltaTime;
    }

    private void ApplyMove()
    {
        if (IsRolling) return;
        _cc.Move((_moveVelocity + Vector3.up * _velocity.y) * Time.deltaTime);
    }

    private void HandleRotation()
    {
        if (IsRolling) return;

        Vector3 lookDir = Vector3.zero;

        if (_lockOn != null && _lockOn.HasTarget)
        {
            lookDir = _lockOn.TargetPosition - transform.position;
        }
        else if (_moveVelocity.sqrMagnitude > 0.01f)
        {
            lookDir = _moveVelocity;
        }

        lookDir.y = 0f;

        if (lookDir.sqrMagnitude > 0.001f)
        {
            float yaw = Quaternion.LookRotation(lookDir).eulerAngles.y;
            Quaternion targetRot = Quaternion.Euler(0f, yaw, 0f);

            float t = 1f - Mathf.Exp(-turnSpeed * 0.01f * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, t);
        }
    }

    public void ApplyRootMotion(Vector3 delta) => _cc.Move(delta);

    public void ResetMotionState()
    {
        _velocity = Vector3.zero;
        _moveVelocity = Vector3.zero;
        _moveVelocityRef = Vector3.zero;
        _coyoteTimer = 0f;
        _jumpBufferTimer = 0f;
        _jumpGraceTimer = 0f;
        _airJumpsRemaining = maxAirJumps;
    }

    public void SnapToGround()
    {
        ResetMotionState();

        Vector3 origin = transform.position + Vector3.up * groundCheckOffset;
        IsGrounded = Physics.CheckSphere(origin, groundCheckRadius, groundMask, QueryTriggerInteraction.Ignore);
        _wasGrounded = IsGrounded;
        if (IsGrounded) _velocity.y = -2f;

        if (_animator != null) _animator.SetFloat(HashSpeed, 0f);
    }

    public void ResetHorizontalMomentum()
    {
        _moveVelocity = Vector3.zero;
        _moveVelocityRef = Vector3.zero;
    }

    public Vector3 GetMoveDirection()
    {
        Vector2 move = _input.MoveInput;
        Vector3 input = new Vector3(move.x, 0f, move.y);
        if (input.sqrMagnitude > 1f) input.Normalize();

        float camYaw = cameraTransform.eulerAngles.y;
        Vector3 camForward = Quaternion.Euler(0f, camYaw, 0f) * Vector3.forward;
        Vector3 camRight = Quaternion.Euler(0f, camYaw, 0f) * Vector3.right;
        return camForward * input.z + camRight * input.x;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = IsGrounded ? Color.green : Color.red;
        Gizmos.DrawWireSphere(transform.position + Vector3.up * groundCheckOffset, groundCheckRadius);
    }
}
