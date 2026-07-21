using UnityEngine;
using UnityEngine.InputSystem;
using Signal.Combat;
using Signal.Combat.Interfaces;
using Signal.UI;

/// <summary>
/// Central input reader. Attach to the player alongside a PlayerInput set to Send Messages.
/// Implements <see cref="ICombatInputSource"/> so combat depends on that abstraction, not this type.
/// </summary>
[RequireComponent(typeof(PlayerInput))]
public class PlayerInputHandler : MonoBehaviour, ICombatInputSource
{
    public Vector2 MoveInput             { get; private set; }
    public Vector2 LookInput             { get; private set; }
    public float   ScrollInput           { get; private set; }

    /// <summary>
    /// True when <see cref="LookInput"/> came from a stick rather than a mouse. The two mean different
    /// things — a stick reports how far it is pushed (a rate), a mouse reports how far it moved (a delta) —
    /// so whoever turns the camera has to integrate one over time and not the other.
    /// </summary>
    public bool    LookIsAnalog          { get; private set; }

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

    private InputAction _jumpAction;
    private InputAction _sprintAction;
    private InputAction _heavyAttackAction;
    private InputAction _lookAction;

    private void Awake()
    {
        PlayerInput playerInput = GetComponent<PlayerInput>();

        // PlayerInput clones the actions asset per player, so saved rebinds must be loaded here too.
        InputBindingStorage.Load(playerInput.actions);

        _jumpAction        = playerInput.actions.FindAction("Jump");
        _sprintAction      = playerInput.actions.FindAction("Sprint");
        _heavyAttackAction = playerInput.actions.FindAction("HeavyAttack");
        _lookAction        = playerInput.actions.FindAction("Look");

        if (_heavyAttackAction == null)
            Debug.LogError("[Combat] PlayerInputHandler: 'HeavyAttack' action not found in the assigned Input Actions asset — heavy attacks will not work.", this);
    }

    private void Update()
    {
        // Poll held states from the actions rather than trusting Send Messages 'canceled' callbacks:
        // a missed release leaves a held flag stuck true, parking hold-to-charge attacks and sprint.
        if (_jumpAction != null)        JumpHeld        = _jumpAction.IsPressed();
        if (_sprintAction != null)      SprintHeld      = _sprintAction.IsPressed();
        if (_heavyAttackAction != null) HeavyAttackHeld = _heavyAttackAction.IsPressed();

        ReadLook();
    }

    /// <summary>
    /// Look has to be polled, not received. Send Messages only fires when a value *changes*, and a stick
    /// held at a constant angle stops changing the instant it gets there — so an event-driven read sees
    /// one push and then silence, and the camera stalls while the player is still holding the stick.
    /// Reading the action every frame gives the stick's current position instead, and a mouse still
    /// reports its per-frame delta the same way it always did.
    /// </summary>
    private void ReadLook()
    {
        if (_lookAction == null) return;

        LookInput = _lookAction.ReadValue<Vector2>();
        InputControl active = _lookAction.activeControl;
        LookIsAnalog = active != null && active.device is Gamepad;
    }

    private void LateUpdate()
    {
        JumpPressedThisFrame     = false;
        DodgePressedThisFrame    = false;
        AttackPressedThisFrame   = false;
        AttackReleasedThisFrame  = false;
        HeavyAttackPressedThisFrame  = false;
        HeavyAttackReleasedThisFrame = false;
        BashPressedThisFrame     = false;
        LockOnPressedThisFrame   = false;
        ScrollInput              = 0f;
        // LookInput isn't cleared here: it's polled fresh every Update, and zeroing it after the fact is
        // what silently swallowed a held stick.
    }

    public void OnMove(InputValue value)
        => MoveInput = value.Get<Vector2>();

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
