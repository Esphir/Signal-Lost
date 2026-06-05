using System.Collections;
using UnityEngine;

/// <summary>
/// Dodge roll with i-frames and perfect-dodge slow-mo.
/// Reads input from PlayerInputHandler.
/// </summary>
public class PlayerDodge : MonoBehaviour
{
    [Header("Roll Settings")]
    public float rollDistance    = 5f;
    public float rollDuration    = 0.35f;
    public float rollCooldown    = 0.6f;

    [Header("I-Frames")]
    public float iFrameDuration  = 0.25f;

    [Header("Perfect Dodge")]
    public float perfectWindow      = 0.12f;
    public float perfectTimeScale   = 0.2f;
    public float perfectSlowDuration = 0.35f;

    // ── State ──────────────────────────────────────────────────────────────
    public bool IsRolling    { get; private set; }
    public bool IsInvincible { get; private set; }

    // ── Private ───────────────────────────────────────────────────────────
    private PlayerController   _controller;
    private PlayerInputHandler _input;

    private float   _cooldownTimer;
    private float   _rollTimer;
    private Vector3 _rollDirection;
    private bool    _perfectActive;

    // ──────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _controller = GetComponent<PlayerController>();
        _input      = GetComponent<PlayerInputHandler>();
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
            dir = -transform.forward;

        _rollDirection = dir.normalized;
        IsRolling      = true;
        _rollTimer     = 0f;
        _cooldownTimer = rollCooldown;

        transform.rotation = Quaternion.LookRotation(_rollDirection);
        StartCoroutine(IFrameWindow());
    }

    private void TickRoll()
    {
        _rollTimer += Time.deltaTime;

        float t          = _rollTimer / rollDuration;
        float speedCurve = Mathf.Sin(t * Mathf.PI);
        float speed      = (rollDistance / rollDuration) * speedCurve;

        _controller.ApplyRootMotion(_rollDirection * speed * Time.deltaTime);

        if (_rollTimer >= rollDuration)
            IsRolling = false;
    }

    private IEnumerator IFrameWindow()
    {
        IsInvincible = true;
        yield return new WaitForSeconds(iFrameDuration);
        IsInvincible = false;
    }

    public bool TryAbsorbHit()
    {
        if (!IsInvincible) return false;
        if (_rollTimer <= perfectWindow && !_perfectActive)
            StartCoroutine(PerfectDodgeSlowMo());
        return true;
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
