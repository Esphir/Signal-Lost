// Dodge roll with i-frames and perfect-dodge slow-mo.
using System.Collections;
using UnityEngine;
using Signal.Combat.Interfaces;

public class PlayerDodge : MonoBehaviour, IInvulnerabilityGate
{
    [Header("Roll Settings")]
    public float rollDistance    = 8f;
    [Tooltip("Fallback duration only — when the roll animation is available, the dodge paces itself to the actual clip length so movement and visuals stay in sync.")]
    public float rollDuration    = 0.35f;
    [Tooltip("Cooldown starts when the roll COMPLETES, not when it starts.")]
    public float rollCooldown    = 0.6f;

    [Header("I-Frames")]
    [Range(0f, 1f)]
    [Tooltip("Normalized point in the roll where invincibility begins (the tuck).")]
    public float iFrameStart = 0.1f;
    [Range(0f, 1f)]
    [Tooltip("Normalized point in the roll where invincibility ends (recovery is vulnerable).")]
    public float iFrameEnd = 0.7f;

    [Header("Control")]
    [Range(0.3f, 1f)]
    [Tooltip("Fraction of the roll ANIMATION after which the player regains control. Mixamo roll clips end with stand-up recovery frames — control returns here and the animation tail blends out under normal locomotion, so there's no post-roll dead time.")]
    public float controlExitNormalized = 0.8f;

    [Header("Perfect Dodge")]
    public float perfectWindow      = 0.12f;
    public float perfectTimeScale   = 0.2f;
    public float perfectSlowDuration = 0.35f;

    public bool IsRolling    { get; private set; }
    public bool IsInvincible { get; private set; }

    public event System.Action DodgeStarted;

    bool IInvulnerabilityGate.IsInvulnerable => IsInvincible;

    private PlayerController      _controller;
    private PlayerInputHandler    _input;
    private PlayerMovementAnimator _movementAnimator;

    private float   _cooldownTimer;
    private float   _rollTimer;
    private float   _activeRollDuration;
    private float   _easedProgress;
    private Vector3 _rollDirection;
    private bool    _perfectActive;

    private void Awake()
    {
        _controller       = GetComponent<PlayerController>();
        _input            = GetComponent<PlayerInputHandler>();
        _movementAnimator = GetComponent<PlayerMovementAnimator>();
    }

    private void Update()
    {
        _cooldownTimer -= Time.deltaTime;

        if (_input.DodgePressedThisFrame && !IsRolling && _cooldownTimer <= 0f)
            StartDodge();

        if (IsRolling)
            TickRoll();
    }

    private void StartDodge()
    {
        Vector3 dir = _controller.GetMoveDirection();
        if (dir.sqrMagnitude < 0.01f)
            dir = transform.forward;

        _rollDirection = dir.normalized;
        IsRolling      = true;
        _rollTimer     = 0f;
        _easedProgress = 0f;
        DodgeStarted?.Invoke();

        _activeRollDuration = rollDuration;
        if (_movementAnimator != null &&
            _movementAnimator.TryPlayRoll(_rollDirection, out float animationDuration))
        {
            _activeRollDuration = animationDuration * controlExitNormalized;
        }

        StartCoroutine(IFrameWindow());
    }

    private void TickRoll()
    {
        _rollTimer += Time.deltaTime;

        float t     = Mathf.Clamp01(_rollTimer / _activeRollDuration);
        float eased = Mathf.SmoothStep(0f, 1f, t);
        _controller.ApplyRootMotion(_rollDirection * ((eased - _easedProgress) * rollDistance));
        _easedProgress = eased;

        if (t >= 1f)
        {
            IsRolling = false;
            _cooldownTimer = rollCooldown;

            _controller.ResetHorizontalMomentum();
        }
    }

    private IEnumerator IFrameWindow()
    {
        float start = _activeRollDuration * Mathf.Min(iFrameStart, iFrameEnd);
        float end   = _activeRollDuration * Mathf.Max(iFrameStart, iFrameEnd);

        if (start > 0f) yield return new WaitForSeconds(start);
        IsInvincible = true;
        yield return new WaitForSeconds(end - start);
        IsInvincible = false;
    }

    public bool TryAbsorbHit()
    {
        if (!IsInvincible) return false;
        if (_rollTimer <= perfectWindow && !_perfectActive)
            StartCoroutine(PerfectDodgeSlowMo());
        return true;
    }

    public void CancelDodge()
    {
        StopAllCoroutines();
        if (_perfectActive)
        {
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;
            _perfectActive = false;
        }
        IsRolling = false;
        IsInvincible = false;
        _rollTimer = 0f;
        _cooldownTimer = 0f;
    }

    private IEnumerator PerfectDodgeSlowMo()
    {
        _perfectActive      = true;
        Time.timeScale      = perfectTimeScale;
        Time.fixedDeltaTime = 0.02f * Time.timeScale;

        yield return new WaitForSecondsRealtime(perfectSlowDuration);

        Time.timeScale      = 1f;
        Time.fixedDeltaTime = 0.02f;
        _perfectActive      = false;
    }
}
