using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Signal.Combat;
using Signal.Combat.Attacks;
using Signal.Combat.Configs;
using Signal.Combat.Data;
using Signal.Combat.Detection;
using Signal.Combat.Interfaces;

/// <summary>
/// Composition root for player combat. Owns no attack logic itself — it wires up the current
/// <see cref="ICombatInputSource"/>, an <see cref="IAttackHitDetector"/> and a
/// <see cref="CombatHitResolver"/> into an <see cref="AttackExecutionContext"/>, then hands control
/// each frame to whichever <see cref="IAttackStrategy"/> reports it can run.
///
/// Adding a new attack type (e.g. a ranged attack) means: write a new IAttackStrategy + config
/// ScriptableObject, add one field + one array slot here. No existing attack code changes.
/// </summary>
public class PlayerCombat : MonoBehaviour, IAttacker
{
    [Header("Attack Configs")]
    [SerializeField] private LightAttackConfigSO lightAttackConfig;
    [SerializeField] private HeavyAttackConfigSO heavyAttackConfig;
    [SerializeField] private KickConfigSO kickConfig;

    [Header("Detection")]
    [SerializeField] private LayerMask hitMask;
    [SerializeField, Min(1)] private int maxHitTargets = 8;

    [Header("Hit-Stop")]
    [SerializeField] private float hitStopScale = 0.05f;

    [Header("Input Buffering")]
    [SerializeField, Min(0f)]
    [Tooltip("Attack presses made while an attack is still playing are remembered this long and fired the instant the current attack unlocks. Keeps combos responsive.")]
    private float inputBufferTime = 0.3f;

    [Header("References")]
    [SerializeField] private CameraShake cameraShake;

    [Header("Upper Body Layer")]
    [SerializeField]
    [Tooltip("Animator layer that plays attack/combat-stance clips over the base locomotion.")]
    private string upperBodyLayerName = "Upper Body";
    [SerializeField, Min(0.01f)]
    [Tooltip("How fast the upper-body layer blends in/out (weight per second). 10 = ~0.1s blend.")]
    private float upperBodyBlendSpeed = 10f;
    [SerializeField, Min(0f)]
    [Tooltip("Seconds the upper-body layer stays blended in after an attack ends, so quick combo hits don't pop back to locomotion between swings.")]
    private float upperBodyHoldTime = 0.35f;

    [SerializeField]
    [Tooltip("Full-body layer that plays the kick over everything. Weight is driven 0→1 only while the kick clip plays, so it never forces bind pose the rest of the time.")]
    private string kickLayerName = "Kick Layer";

    // ── IAttacker ────────────────────────────────────────────────────────────
    public bool IsAttacking { get; private set; }
    public event Action<int> AttackLanded;

    // ── Private ───────────────────────────────────────────────────────────
    private PlayerController _controller;
    private PlayerDodge _dodge;
    private ICombatInputSource _input;
    private Animator _animator;

    private IAttackStrategy[] _strategies;
    private AttackExecutionContext _context;
    private bool _hitStopActive;

    private int _upperBodyLayerIndex = -1;
    private float _upperBodyHoldTimer;
    private float _lastUpperBodyTarget;

    private int _kickLayerIndex = -1;

    private int _bufferedStrategyIndex = -1;
    private float _bufferedUntil;

    private void Awake()
    {
        _controller = GetComponent<PlayerController>();
        _dodge = GetComponent<PlayerDodge>();
        _input = GetComponent<ICombatInputSource>();
        _animator = GetComponentInChildren<Animator>();

        if (!ValidateSetup())
        {
            enabled = false;
            return;
        }

        _context = new AttackExecutionContext
        {
            Origin = transform,
            Instigator = gameObject,
            Animator = _animator,
            HitMask = hitMask,
            HitDetector = new OverlapSphereHitDetector(maxHitTargets),
            Resolver = new CombatHitResolver(),
            ValidAnimatorParameters = CacheAnimatorParameters(),
            ApplyRootMotion = delta => _controller?.ApplyRootMotion(delta),
            TriggerHitStop = duration => StartCoroutine(HitStopRoutine(duration)),
            TriggerCameraShake = (amount, duration) => cameraShake?.Shake(amount, duration),
            OnAttackLanded = hits => AttackLanded?.Invoke(hits)
        };

        // Layer index must be resolved before trigger/state validation can check HasState.
        InitializeUpperBodyLayer();
        InitializeKickLayer();
        WarnAboutMissingTriggers();
        _context.AttackTriggerHashes = CollectAttackTriggerHashes();

        // Order matters only in that each strategy owns its own input check (different buttons),
        // so any order is safe. Kick first purely by convention (crowd-control takes priority read).
        _strategies = new IAttackStrategy[]
        {
            new KickStrategy(kickConfig),
            new HeavyAttackStrategy(heavyAttackConfig),
            new LightAttackStrategy(lightAttackConfig),
        };
    }

    private void Update()
    {
        UpdateUpperBodyLayer();
        UpdateKickLayer();

        for (int i = 0; i < _strategies.Length; i++)
            _strategies[i].Tick(Time.deltaTime);

        if (_dodge != null && _dodge.IsRolling) return;

        if (IsAttacking)
        {
            // Remember presses made mid-attack so they fire the frame the current attack unlocks.
            for (int i = 0; i < _strategies.Length; i++)
            {
                if (_strategies[i].CanExecute(_input))
                {
                    _bufferedStrategyIndex = i;
                    _bufferedUntil = Time.time + inputBufferTime;
                    CombatLog.Info($"Buffered next attack: {_strategies[i].GetType().Name}", this);
                    break;
                }
            }
            return;
        }

        // A press buffered during the previous attack's tail wins over fresh input this frame.
        if (_bufferedStrategyIndex >= 0)
        {
            int buffered = _bufferedStrategyIndex;
            bool stillFresh = Time.time <= _bufferedUntil;
            _bufferedStrategyIndex = -1;
            if (stillFresh)
            {
                StartCoroutine(RunAttack(_strategies[buffered]));
                return;
            }
        }

        for (int i = 0; i < _strategies.Length; i++)
        {
            if (_strategies[i].CanExecute(_input))
            {
                StartCoroutine(RunAttack(_strategies[i]));
                break;
            }
        }
    }

    private IEnumerator RunAttack(IAttackStrategy strategy)
    {
        IsAttacking = true;
        CombatLog.Info($"Attack started: {strategy.GetType().Name}", this);
        try
        {
            yield return strategy.Execute(_context, _input);
        }
        finally
        {
            // Always release the attack lock, even if a strategy throws mid-swing — otherwise a
            // single bad config would leave IsAttacking stuck true and silently disable all combat.
            IsAttacking = false;
            CombatLog.Info($"Attack finished: {strategy.GetType().Name}", this);
        }
    }

    // ── Validation ────────────────────────────────────────────────────────

    private bool ValidateSetup()
    {
        bool ok = true;

        if (lightAttackConfig == null) { Debug.LogError("[Combat] PlayerCombat: 'Light Attack Config' is not assigned in the Inspector.", this); ok = false; }
        if (heavyAttackConfig == null) { Debug.LogError("[Combat] PlayerCombat: 'Heavy Attack Config' is not assigned in the Inspector.", this); ok = false; }
        if (kickConfig == null)        { Debug.LogError("[Combat] PlayerCombat: 'Kick Config' is not assigned in the Inspector.", this); ok = false; }
        if (_input == null)            { Debug.LogError("[Combat] PlayerCombat: no ICombatInputSource (PlayerInputHandler) found on this GameObject.", this); ok = false; }

        if (_animator == null)
            Debug.LogWarning("[Combat] PlayerCombat: no Animator found in children — attacks will run without animations.", this);

        if (hitMask.value == 0)
            Debug.LogWarning("[Combat] PlayerCombat: 'Hit Mask' is set to Nothing — attacks will never hit anything.", this);

        if (!ok)
            Debug.LogError("[Combat] PlayerCombat disabled — assign the missing references above, then re-enter Play Mode.", this);

        return ok;
    }

    private HashSet<int> CacheAnimatorParameters()
    {
        if (_animator == null) return null;

        var hashes = new HashSet<int>();
        foreach (AnimatorControllerParameter parameter in _animator.parameters)
            hashes.Add(parameter.nameHash);
        return hashes;
    }

    /// <summary>
    /// Distinct, valid trigger hashes across the whole moveset (light combo chain + heavy + kick).
    /// Used to clear stale queued triggers before each swing so spam-clicking can't stack phantom attacks.
    /// </summary>
    private int[] CollectAttackTriggerHashes()
    {
        var hashes = new HashSet<int>();

        void Add(AttackConfigBaseSO config)
        {
            if (config == null || string.IsNullOrEmpty(config.animatorTrigger)) return;
            if (_context.ValidAnimatorParameters == null ||
                _context.ValidAnimatorParameters.Contains(config.AnimatorTriggerHash))
                hashes.Add(config.AnimatorTriggerHash);
        }

        LightAttackConfigSO step = lightAttackConfig;
        for (int i = 0; step != null && i < 16; i++)
        {
            Add(step);
            step = step.nextComboStep;
            if (step == lightAttackConfig) break;
        }
        Add(heavyAttackConfig);
        Add(kickConfig);

        var result = new int[hashes.Count];
        hashes.CopyTo(result);
        return result;
    }

    private void WarnAboutMissingTriggers()
    {
        // Walk the light combo chain so a bad trigger on step 3 is reported at startup instead of
        // three swings into a fight. Bounded in case of an accidental cycle in the chain assets.
        LightAttackConfigSO step = lightAttackConfig;
        for (int i = 0; step != null && i < 16; i++)
        {
            ReportTriggerState(step);
            step = step.nextComboStep;
            if (step == lightAttackConfig) break;
        }

        ReportTriggerState(heavyAttackConfig);
        ReportTriggerState(kickConfig);
    }

    private void ReportTriggerState(AttackConfigBaseSO config)
    {
        if (string.IsNullOrEmpty(config.animatorTrigger))
        {
            CombatLog.Info($"Attack '{config.name}' has no animator trigger configured — it will play without an animation.", this);
            return;
        }

        if (_context.ValidAnimatorParameters != null &&
            !_context.ValidAnimatorParameters.Contains(config.AnimatorTriggerHash))
        {
            Debug.LogError(
                $"[Combat] Animator is missing trigger '{config.animatorTrigger}' required by attack '{config.name}' — the attack will still deal damage but never animate.",
                this);
        }

        if (config.HasAnimatorState && _animator != null && !StateExistsOnAnyLayer(config.AnimatorStateHash))
        {
            Debug.LogError(
                $"[Combat] Animator has no state '{config.animatorStateName}' on any layer (attack '{config.name}') — animation-synced timing will fall back to the fixed startup/recovery times.",
                this);
        }
    }

    /// <summary>True if any animator layer's state machine contains the given state hash (attacks may live on different layers).</summary>
    private bool StateExistsOnAnyLayer(int stateHash)
    {
        for (int layer = 0; layer < _animator.layerCount; layer++)
            if (_animator.HasState(layer, stateHash)) return true;
        return false;
    }

    // ── Upper-body layer blending ─────────────────────────────────────────

    private void InitializeUpperBodyLayer()
    {
        if (_animator == null) return;

        _upperBodyLayerIndex = _animator.GetLayerIndex(upperBodyLayerName);
        if (_upperBodyLayerIndex < 0)
        {
            CombatLog.Warn($"Animator has no '{upperBodyLayerName}' layer — upper-body blending disabled.", this);
            return;
        }

        // The controller authors this Override layer at weight 1, which makes CombatIdle
        // permanently replace the top half of idle/walk. Start relaxed instead — the weight is
        // blended up only while attacking, so full-body locomotion shows the rest of the time.
        // (The full-body Kick Layer is self-managing via its WriteDefaults-off Empty state, so it
        // needs no weight driving here; the strategies resolve each attack's layer for timing.)
        _animator.SetLayerWeight(_upperBodyLayerIndex, 0f);
    }

    private void UpdateUpperBodyLayer()
    {
        if (_upperBodyLayerIndex < 0) return;

        if (IsAttacking) _upperBodyHoldTimer = upperBodyHoldTime;
        else if (_upperBodyHoldTimer > 0f) _upperBodyHoldTimer -= Time.deltaTime;

        float target = (IsAttacking || _upperBodyHoldTimer > 0f) ? 1f : 0f;

        if (!Mathf.Approximately(target, _lastUpperBodyTarget))
        {
            CombatLog.Info(target > 0f ? "Upper-body layer blending IN (attack stance)" : "Upper-body layer blending OUT (locomotion)", this);
            _lastUpperBodyTarget = target;
        }

        float current = _animator.GetLayerWeight(_upperBodyLayerIndex);
        if (!Mathf.Approximately(current, target))
            _animator.SetLayerWeight(_upperBodyLayerIndex,
                Mathf.MoveTowards(current, target, upperBodyBlendSpeed * Time.deltaTime));
    }

    // ── Kick layer blending ───────────────────────────────────────────────

    private void InitializeKickLayer()
    {
        if (_animator == null) return;

        _kickLayerIndex = _animator.GetLayerIndex(kickLayerName);
        if (_kickLayerIndex < 0)
        {
            CombatLog.Warn($"Animator has no '{kickLayerName}' layer — kick will play on its config's fallback layer.", this);
            return;
        }

        // Full-body Override layer: it must sit at weight 0 when idle, otherwise its Empty state
        // forces the whole rig to bind pose (T-pose). Weight is raised only while the kick plays.
        _animator.SetLayerWeight(_kickLayerIndex, 0f);
    }

    private void UpdateKickLayer()
    {
        if (_kickLayerIndex < 0 || kickConfig == null) return;

        // Show the kick only while its clip is actually playing and before its exit point, so the
        // weight is back to 0 by the time the layer returns to its Empty (pass-through) state.
        bool showing = _context.TryGetAttackNormalizedTime(kickConfig, out float t)
                       && t < kickConfig.exitNormalizedTime;
        float target = showing ? 1f : 0f;

        float current = _animator.GetLayerWeight(_kickLayerIndex);
        if (!Mathf.Approximately(current, target))
            _animator.SetLayerWeight(_kickLayerIndex,
                Mathf.MoveTowards(current, target, upperBodyBlendSpeed * Time.deltaTime));
    }

    // ── Hit-stop ──────────────────────────────────────────────────────────

    private IEnumerator HitStopRoutine(float duration)
    {
        if (_hitStopActive) yield break;
        _hitStopActive = true;

        Time.timeScale = hitStopScale;
        Time.fixedDeltaTime = 0.02f * Time.timeScale;

        yield return new WaitForSecondsRealtime(duration);

        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
        _hitStopActive = false;
    }

    private void OnDrawGizmosSelected()
    {
        // Live inspector values straight from the config assets:
        // red = light, orange = heavy (full-charge radius), blue = kick.
        Vector3 origin = transform.position + Vector3.up * 0.8f;
        DrawAttackRangeGizmo(lightAttackConfig, Color.red, origin);
        DrawAttackRangeGizmo(heavyAttackConfig, new Color(1f, 0.5f, 0f), origin);
        DrawAttackRangeGizmo(kickConfig, Color.blue, origin);
    }

    private void DrawAttackRangeGizmo(AttackConfigBaseSO config, Color color, Vector3 origin)
    {
        if (config == null) return;
        Gizmos.color = color;
        Gizmos.DrawWireSphere(origin + transform.forward * config.hitOffset, config.hitRadius);
    }
}
