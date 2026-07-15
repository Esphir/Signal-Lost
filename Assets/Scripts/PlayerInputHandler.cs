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

    private void Awake()
    {
        PlayerInput playerInput = GetComponent<PlayerInput>();

        // PlayerInput clones the actions asset per player, so saved rebinds must be loaded here too.
        InputBindingStorage.Load(playerInput.actions);

        _jumpAction        = playerInput.actions.FindAction("Jump");
        _sprintAction      = playerInput.actions.FindAction("Sprint");
        _heavyAttackAction = playerInput.actions.FindAction("HeavyAttack");

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
        LookInput                = Vector2.zero; // mouse delta is per-frame; clear it each frame
        ScrollInput              = 0f;
    }

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
