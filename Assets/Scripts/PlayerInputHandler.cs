using UnityEngine;
using UnityEngine.InputSystem;

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
///   Attack      - Button
///   Kick        - Button
///   LockOn      - Button
/// </summary>
[RequireComponent(typeof(PlayerInput))]
public class PlayerInputHandler : MonoBehaviour
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
    public bool    KickPressedThisFrame     { get; private set; }
    public bool    LockOnPressedThisFrame   { get; private set; }

    // ──────────────────────────────────────────────────────────────────────

    private void LateUpdate()
    {
        // Clear single-frame flags
        JumpPressedThisFrame     = false;
        DodgePressedThisFrame    = false;
        AttackPressedThisFrame   = false;
        AttackReleasedThisFrame  = false;
        KickPressedThisFrame     = false;
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

    public void OnKick(InputValue value)
    {
        if (value.isPressed) KickPressedThisFrame = true;
    }

    public void OnLockOn(InputValue value)
    {
        if (value.isPressed) LockOnPressedThisFrame = true;
    }
}
