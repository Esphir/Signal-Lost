using UnityEngine;
using UnityEngine.InputSystem;
using Signal.Combat;
using Signal.Combat.Interfaces;
using Signal.UI;

/// <summary>
/// Central input reader. Attach to the player alongside all other player scripts.
/// Requires a PlayerInput component set to Send Messages behaviour.
///
/// Input Actions needed:
///   Move        - Value / Vector2
///   Look        - Value / Vector2  (bind to Mouse Delta + Right Stick)
///   Scroll      - Value / Vector2  (bind to Mouse Scroll)
///   Jump        - Button
///   Sprint      - Button
///   Dodge       - Button
///   Attack      - Button           (light attack — Left Mouse Button)
///   HeavyAttack - Button           (Right Mouse Button)
///   Bash        - Button           (F)
///   LockOn      - Button
///
/// Implements ICombatInputSource so combat code depends on that abstraction rather than this
/// concrete MonoBehaviour.
/// </summary>
[RequireComponent(typeof(PlayerInput))]
public class PlayerInputHandler : MonoBehaviour, ICombatInputSource
{
    // ── Polled state ──────────────────────────────────────────────────────
    public Vector2 MoveInput             { get; private set; }
    public Vector2 LookInput             { get; private set; }   // mouse delta / right stick
    public float   ScrollInput           { get; private set; }   // mouse wheel y

    public bool    JumpHeld              { get; private set; }
    public bool    JumpPressedThisFrame  { get; private set; }
    public bool    SprintHeld            { get; private set; }

    public bool    DodgePressedThisFrame    { get; private set; }
    public bool    AttackPressed            { get; private set; }
    public bool    AttackPressedThisFrame   { get; private set; }
    public bool    AttackReleasedThisFrame  { get; private set; }
    public bool    HeavyAttackHeld              { get; private set; }
    public bool    HeavyAttackPressedThisFrame   { get; private set; }
    public bool    HeavyAttackReleasedThisFrame  { get; private set; }
    public bool    BashPressedThisFrame     { get; private set; }
    public bool    LockOnPressedThisFrame   { get; private set; }

    // ── Actions polled directly (held state) ─────────────────────────────
    private InputAction _jumpAction;
    private InputAction _sprintAction;
    private InputAction _heavyAttackAction;

    // ──────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        PlayerInput playerInput = GetComponent<PlayerInput>();

        // PlayerInput uses a per-player clone of the actions asset, so saved rebinds must be
        // applied here as well as in the menu.
        InputBindingStorage.Load(playerInput.actions);

        _jumpAction        = playerInput.actions.FindAction("Jump");
        _sprintAction      = playerInput.actions.FindAction("Sprint");
        _heavyAttackAction = playerInput.actions.FindAction("HeavyAttack");

        if (_heavyAttackAction == null)
            Debug.LogError("[Combat] PlayerInputHandler: 'HeavyAttack' action not found in the assigned Input Actions asset — heavy attacks will not work.", this);
    }

    private void Update()
    {
        // Held states are polled from the actions each frame rather than trusting Send Messages
        // release callbacks: Button-action 'canceled' messages proved unreliable here, and a
        // missed release leaves a held flag stuck true forever — which parks hold-to-charge
        // attacks in their wait loop (and sticks sprint / low-jump gravity). Coroutines resume
        // after Update, so charge loops always read the real button state.
        if (_jumpAction != null)        JumpHeld        = _jumpAction.IsPressed();
        if (_sprintAction != null)      SprintHeld      = _sprintAction.IsPressed();
        if (_heavyAttackAction != null) HeavyAttackHeld = _heavyAttackAction.IsPressed();
    }

    private void LateUpdate()
    {
        // Clear single-frame flags
        JumpPressedThisFrame     = false;
        DodgePressedThisFrame    = false;
        AttackPressedThisFrame   = false;
        AttackReleasedThisFrame  = false;
        HeavyAttackPressedThisFrame  = false;
        HeavyAttackReleasedThisFrame = false;
        BashPressedThisFrame     = false;
        LockOnPressedThisFrame   = false;
        LookInput                = Vector2.zero;  // mouse delta resets each frame
        ScrollInput              = 0f;
    }

    // ── Input System Send Messages callbacks ──────────────────────────────

    public void OnMove(InputValue value)
        => MoveInput = value.Get<Vector2>();

    public void OnLook(InputValue value)
        => LookInput = value.Get<Vector2>();

    public void OnScroll(InputValue value)
        => ScrollInput = value.Get<Vector2>().y;

    public void OnJump(InputValue value)
    {
        JumpHeld = value.isPressed;
        if (value.isPressed) JumpPressedThisFrame = true;
    }

    public void OnSprint(InputValue value)
        => SprintHeld = value.isPressed;

    public void OnDodge(InputValue value)
    {
        if (value.isPressed) DodgePressedThisFrame = true;
    }

    public void OnAttack(InputValue value)
    {
        AttackPressed = value.isPressed;
        if (value.isPressed)  AttackPressedThisFrame  = true;
        if (!value.isPressed) AttackReleasedThisFrame = true;
    }

    public void OnHeavyAttack(InputValue value)
    {
        HeavyAttackHeld = value.isPressed;
        if (value.isPressed)  HeavyAttackPressedThisFrame  = true;
        if (!value.isPressed) HeavyAttackReleasedThisFrame = true;
        CombatLog.Info(value.isPressed ? "HeavyAttack input pressed" : "HeavyAttack input released", this);
    }

    public void OnBash(InputValue value)
    {
        if (value.isPressed) BashPressedThisFrame = true;
    }

    public void OnLockOn(InputValue value)
    {
        if (value.isPressed) LockOnPressedThisFrame = true;
    }
}
