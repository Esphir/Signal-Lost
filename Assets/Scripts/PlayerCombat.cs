using System.Collections;
using UnityEngine;

/// <summary>
/// Melee combo system with hit-stop, screen shake, and charge attack.
/// Reads input from PlayerInputHandler.
/// </summary>
public class PlayerCombat : MonoBehaviour
{
    [Header("Combo")]
    public int   maxComboSteps  = 3;
    public float comboWindow    = 0.5f;
    public float attackLunge    = 1.5f;

    [Header("Damage")]
    public float lightDamage    = 10f;
    public float heavyDamage    = 35f;
    public float hitRadius      = 1.2f;
    public float hitOffset      = 1.0f;
    public LayerMask hitMask;

    [Header("Charge Attack")]
    public float chargeThreshold = 0.5f;
    public float chargeLunge     = 4f;

    [Header("Hit-Stop")]
    public float hitStopDuration = 0.06f;
    public float hitStopScale    = 0.05f;

    [Header("Screen Shake")]
    public CameraShake cameraShake;
    public float lightShakeAmount   = 0.1f;
    public float lightShakeDuration = 0.1f;
    public float heavyShakeAmount   = 0.3f;
    public float heavyShakeDuration = 0.2f;

    // ── State ──────────────────────────────────────────────────────────────
    public bool IsAttacking { get; private set; }

    // ── Private ───────────────────────────────────────────────────────────
    private PlayerController   _controller;
    private PlayerDodge        _dodge;
    private PlayerInputHandler _input;

    private int   _comboStep;
    private float _comboTimer;
    private float _attackHoldTimer;
    private bool  _attackHeld;
    private bool  _hitStopActive;

    // ──────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _controller = GetComponent<PlayerController>();
        _dodge      = GetComponent<PlayerDodge>();
        _input      = GetComponent<PlayerInputHandler>();
    }

    private void Update()
    {
        if (_comboTimer > 0f)
        {
            _comboTimer -= Time.deltaTime;
            if (_comboTimer <= 0f) _comboStep = 0;
        }

        HandleAttackInput();
        HandleKickInput();
    }

    private void HandleAttackInput()
    {
        if (_dodge != null && _dodge.IsRolling) return;
        if (IsAttacking) return;

        if (_input.AttackPressedThisFrame)
        {
            _attackHoldTimer = 0f;
            _attackHeld      = true;
        }

        if (_attackHeld)
            _attackHoldTimer += Time.deltaTime;

        if (_input.AttackReleasedThisFrame && _attackHeld)
        {
            _attackHeld = false;
            if (_attackHoldTimer >= chargeThreshold)
                StartCoroutine(PerformHeavyAttack());
            else
                StartCoroutine(PerformLightAttack());
        }
    }

    private void HandleKickInput()
    {
        if (_input.KickPressedThisFrame && !IsAttacking)
            StartCoroutine(PerformKick());
    }

    private IEnumerator PerformLightAttack()
    {
        IsAttacking = true;
        _comboTimer = 0f;

        _controller.ApplyRootMotion(transform.forward * attackLunge * Time.deltaTime * 10f);

        float attackDuration = _comboStep == maxComboSteps - 1 ? 0.25f : 0.18f;
        yield return new WaitForSeconds(attackDuration * 0.4f);

        bool hit = DealDamage(lightDamage, _comboStep == maxComboSteps - 1);
        if (hit)
        {
            yield return StartCoroutine(DoHitStop(hitStopDuration));
            cameraShake?.Shake(lightShakeAmount, lightShakeDuration);
        }

        yield return new WaitForSeconds(attackDuration * 0.6f);

        _comboStep  = (_comboStep + 1) % maxComboSteps;
        _comboTimer = comboWindow;
        IsAttacking = false;
    }

    private IEnumerator PerformHeavyAttack()
    {
        IsAttacking = true;
        yield return new WaitForSeconds(0.15f);

        _controller.ApplyRootMotion(transform.forward * chargeLunge * Time.deltaTime * 10f);

        bool hit = DealDamage(heavyDamage, true);
        if (hit)
        {
            yield return StartCoroutine(DoHitStop(hitStopDuration * 1.5f));
            cameraShake?.Shake(heavyShakeAmount, heavyShakeDuration);
        }

        yield return new WaitForSeconds(0.35f);
        _comboStep  = 0;
        IsAttacking = false;
    }

    private IEnumerator PerformKick()
    {
        IsAttacking = true;
        yield return new WaitForSeconds(0.1f);

        Collider[] hits = Physics.OverlapSphere(
            transform.position + transform.forward * hitOffset + Vector3.up * 0.8f,
            hitRadius, hitMask);

        foreach (Collider col in hits)
        {
            col.SendMessage("OnKick", SendMessageOptions.DontRequireReceiver);
            Rigidbody rb = col.GetComponent<Rigidbody>();
            if (rb != null) rb.AddForce(transform.forward * 8f + Vector3.up * 5f, ForceMode.Impulse);
        }

        if (hits.Length > 0)
        {
            yield return StartCoroutine(DoHitStop(hitStopDuration));
            cameraShake?.Shake(lightShakeAmount, lightShakeDuration);
        }

        yield return new WaitForSeconds(0.25f);
        IsAttacking = false;
    }

    public bool DealDamage(float amount, bool isLauncher = false)
    {
        Vector3 hitCenter = transform.position + transform.forward * hitOffset + Vector3.up * 0.8f;
        Collider[] hits   = Physics.OverlapSphere(hitCenter, hitRadius, hitMask);
        if (hits.Length == 0) return false;

        foreach (Collider col in hits)
        {
            col.SendMessage("TakeDamage", amount, SendMessageOptions.DontRequireReceiver);
            if (isLauncher)
                col.SendMessage("OnLaunched", SendMessageOptions.DontRequireReceiver);
        }

        return true;
    }

    private IEnumerator DoHitStop(float duration)
    {
        if (_hitStopActive) yield break;
        _hitStopActive      = true;
        Time.timeScale      = hitStopScale;
        Time.fixedDeltaTime = 0.02f * Time.timeScale;

        yield return new WaitForSecondsRealtime(duration);

        Time.timeScale      = 1f;
        Time.fixedDeltaTime = 0.02f;
        _hitStopActive      = false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(
            transform.position + transform.forward * hitOffset + Vector3.up * 0.8f, hitRadius);
    }
}
