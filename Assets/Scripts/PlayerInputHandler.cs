// Central input reader.
using UnityEngine;
using UnityEngine.InputSystem;
using Signal.Combat;
using Signal.Combat.Interfaces;
using Signal.UI;

[RequireComponent(typeof(PlayerInput))]
public class PlayerInputHandler : MonoBehaviour, ICombatInputSource
{
    public Vector2 MoveInput             { get; private set; }
    public Vector2 LookInput             { get; private set; }
    public float   ScrollInput           { get; private set; }

    public bool    LookIsAnalog          { get; private set; }

    public bool    JumpHeld              { get; private set; }
    public bool    JumpPressedThisFrame  { get; private set; }

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
    private InputAction _heavyAttackAction;
    private InputAction _lookAction;

    private void Awake()
    {
        PlayerInput playerInput = GetComponent<PlayerInput>();

        InputBindingStorage.Load(playerInput.actions);

        _jumpAction        = playerInput.actions.FindAction("Jump");
        _heavyAttackAction = playerInput.actions.FindAction("HeavyAttack");
        _lookAction        = playerInput.actions.FindAction("Look");

        if (_heavyAttackAction == null)
            Debug.LogError("[Combat] PlayerInputHandler: 'HeavyAttack' action not found in the assigned Input Actions asset — heavy attacks will not work.", this);
    }

    private void Update()
    {
        if (_jumpAction != null)        JumpHeld        = _jumpAction.IsPressed();
        if (_heavyAttackAction != null) HeavyAttackHeld = _heavyAttackAction.IsPressed();

        ReadLook();
    }

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
