using System.Collections.Generic;
using UnityEngine;
using KevinIglesias;
using Signal.Combat;

/// <summary>
/// Drives the full-body "Movement" animator layer (jump / falling loop / directional rolls),
/// blending it in only while airborne or rolling.
///
/// Suspends the <see cref="SpineProxy"/> while this layer dominates: the Mixamo movement clips
/// have no proxy-bone curves, so the proxy would otherwise stomp the rolling/falling spine into
/// the locomotion pose every LateUpdate. Resumes as the layer blends back out.
/// </summary>
public class PlayerMovementAnimator : MonoBehaviour
{
    [SerializeField]
    [Tooltip("Animator layer holding the jump/fall/roll states.")]
    private string movementLayerName = "Movement";

    [SerializeField, Min(0.01f)]
    [Tooltip("How fast the layer blends in/out (weight per second).")]
    private float layerBlendSpeed = 12f;

    [SerializeField, Range(0f, 1f)]
    [Tooltip("Layer weight above which the SpineProxy copy is suspended so movement clips fully own the spine.")]
    private float spineProxySuspendWeight = 0.5f;

    private static readonly int HashIsGrounded = Animator.StringToHash("IsGrounded");
    private static readonly int HashVerticalVelocity = Animator.StringToHash("VerticalVelocity");
    private static readonly int HashRollDirection = Animator.StringToHash("RollDirection");
    private static readonly int HashRollTrigger = Animator.StringToHash("RollTrigger");

    // RollDirection values — must match the Movement layer's AnyState transitions.
    private const int RollForward = 0, RollBackward = 1, RollLeft = 2, RollRight = 3;

    // Clip names inside the roll FBXs, used to look up real animation lengths at startup.
    private static readonly (int direction, string clipName)[] RollClips =
    {
        (RollForward, "RollForward"),
        (RollBackward, "RollBackward"),
        (RollLeft, "RollLeft"),
        (RollRight, "RollRight"),
    };

    private Animator _animator;
    private PlayerController _controller;
    private PlayerDodge _dodge;      // optional
    private SpineProxy _spineProxy;  // optional
    private int _layerIndex = -1;
    private readonly Dictionary<int, float> _rollClipLengths = new Dictionary<int, float>();

    private void Awake()
    {
        _animator = GetComponentInChildren<Animator>();
        _controller = GetComponent<PlayerController>();
        _dodge = GetComponent<PlayerDodge>();
        _spineProxy = GetComponentInChildren<SpineProxy>();

        if (_animator == null || _controller == null)
        {
            Debug.LogWarning("[Combat] PlayerMovementAnimator needs an Animator (in children) and a PlayerController — disabled.", this);
            enabled = false;
            return;
        }

        _layerIndex = _animator.GetLayerIndex(movementLayerName);
        if (_layerIndex < 0)
        {
            Debug.LogWarning($"[Combat] Animator has no '{movementLayerName}' layer — movement animations disabled.", this);
            enabled = false;
            return;
        }

        _animator.SetLayerWeight(_layerIndex, 0f);
        CacheRollClipLengths();
    }

    private void OnDisable()
    {
        // Never leave the proxy suspended if this component is torn down while airborne/rolling.
        SpineProxyGate.SetSuspended(_spineProxy, this, false);
    }

    private void Update()
    {
        _animator.SetBool(HashIsGrounded, _controller.IsGrounded);
        _animator.SetFloat(HashVerticalVelocity, _controller.Velocity.y);

        bool active = !_controller.IsGrounded || (_dodge != null && _dodge.IsRolling);
        float target = active ? 1f : 0f;
        float weight = _animator.GetLayerWeight(_layerIndex);
        if (!Mathf.Approximately(weight, target))
        {
            weight = Mathf.MoveTowards(weight, target, layerBlendSpeed * Time.deltaTime);
            _animator.SetLayerWeight(_layerIndex, weight);
        }

        // Via the gate (not SpineProxy.enabled directly) since PlayerCombat suspends it too.
        if (_spineProxy != null)
            SpineProxyGate.SetSuspended(_spineProxy, this, weight >= spineProxySuspendWeight);
    }

    /// <summary>
    /// Plays the roll animation matching a world-space direction (resolved against current facing)
    /// and reports the real clip length so gameplay can pace the dodge to the animation.
    /// Returns false when unavailable — callers should fall back to their own timing.
    /// </summary>
    public bool TryPlayRoll(Vector3 worldDirection, out float animationDuration)
    {
        animationDuration = 0f;
        if (!enabled) return false;

        Vector3 local = transform.InverseTransformDirection(worldDirection);
        local.y = 0f;

        int direction = Mathf.Abs(local.z) >= Mathf.Abs(local.x)
            ? (local.z >= 0f ? RollForward : RollBackward)
            : (local.x >= 0f ? RollRight : RollLeft);

        _animator.SetInteger(HashRollDirection, direction);
        _animator.SetTrigger(HashRollTrigger);

        _rollClipLengths.TryGetValue(direction, out animationDuration);
        CombatLog.Info($"Roll animation: {(direction == 0 ? "Forward" : direction == 1 ? "Backward" : direction == 2 ? "Left" : "Right")} ({animationDuration:0.##}s)", this);
        return animationDuration > 0.05f;
    }

    private void CacheRollClipLengths()
    {
        AnimationClip[] clips = _animator.runtimeAnimatorController != null
            ? _animator.runtimeAnimatorController.animationClips
            : null;
        if (clips == null) return;

        foreach ((int direction, string clipName) in RollClips)
        {
            foreach (AnimationClip clip in clips)
            {
                if (clip != null && clip.name == clipName)
                {
                    _rollClipLengths[direction] = clip.length;
                    break;
                }
            }

            if (!_rollClipLengths.ContainsKey(direction))
                Debug.LogWarning($"[Combat] Roll clip '{clipName}' not found in the animator — that direction will use the dodge's fallback duration.", this);
        }
    }
}
