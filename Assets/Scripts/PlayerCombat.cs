using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using KevinIglesias;
using Signal.Combat;
using Signal.Combat.Attacks;
using Signal.Combat.Configs;
using Signal.Combat.Data;
using Signal.Combat.Detection;
using Signal.Combat.Interfaces;
using Signal.Run;

/// <summary>
/// Composition root for player combat. Owns no attack logic itself: it wires up the
/// <see cref="ICombatInputSource"/>, an <see cref="IAttackHitDetector"/> and a
/// <see cref="CombatHitResolver"/> into an <see cref="AttackExecutionContext"/>, then hands control
/// each frame to whichever <see cref="IAttackStrategy"/> reports it can run. Adding an attack type
/// means a new IAttackStrategy + config and one array slot here — no existing attack code changes.
/// </summary>
public class PlayerCombat : MonoBehaviour, IAttacker
{
    [Header("Attack Configs")]
    [SerializeField] private LightAttackConfigSO lightAttackConfig;
    [SerializeField] private HeavyAttackConfigSO heavyAttackConfig;
    [SerializeField, FormerlySerializedAs("kickConfig")] private BashConfigSO bashConfig;

    [Header("Detection")]
    [SerializeField] private LayerMask hitMask;
    [SerializeField, Min(1)] private int maxHitTargets = 8;

    [Header("Hit-Stop")]
    [SerializeField] private float hitStopScale = 0.05f;

    [Header("Critical Hits")]
    [SerializeField, Min(1f)]
    [Tooltip("Damage multiplier when an attack rolls a critical hit (crit chance comes from run upgrades).")]
    private float critDamageMultiplier = 2f;

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

    [SerializeField, FormerlySerializedAs("kickLayerName")]
    [Tooltip("Full-body layer that plays the standing bash over everything. Weight is driven 0→1 only while the standing bash clip plays, so it never forces bind pose the rest of the time.")]
    private string bashLayerName = "Bash Layer";

    [SerializeField, Range(0f, 1f)]
    [Tooltip("Bash-layer weight above which the SpineProxy copy is suspended so the full-body standing bash owns the spine. Resumes automatically as the layer blends back out.")]
    private float spineProxySuspendWeight = 0.5f;

    public bool IsAttacking { get; private set; }
    public event Action<int> AttackLanded;

    /// <summary>Raised with the damage dealt each time an attack sweep connects (life steal hooks in here).</summary>
    public event Action<float> DamageDealt;

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

    private int _bashLayerIndex = -1;
    private SpineProxy _spineProxy;

    private int _bufferedStrategyIndex = -1;
    private float _bufferedUntil;

    private void Awake()
    {
        _controller = GetComponent<PlayerController>();
        _dodge = GetComponent<PlayerDodge>();
        _input = GetComponent<ICombatInputSource>();
        _animator = GetComponentInChildren<Animator>();
        _spineProxy = GetComponentInChildren<SpineProxy>();

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
            GetPlanarSpeed = () =>
            {
                if (_controller == null) return 0f;
                Vector3 v = _controller.Velocity;
                v.y = 0f;
                return v.magnitude;
            },
            GetStat = RunManager.QueryStat,
            CriticalMultiplier = critDamageMultiplier,
            OnCriticalHit = position => CombatLog.Info("Critical hit!", this),
            OnDamageDealt = amount => DamageDealt?.Invoke(amount),
            TriggerHitStop = duration => StartCoroutine(HitStopRoutine(duration)),
            TriggerCameraShake = (amount, duration) => cameraShake?.Shake(amount, duration),
            OnAttackLanded = hits => AttackLanded?.Invoke(hits)
        };

        // Layer index must be resolved before trigger/state validation can check HasState.
        InitializeUpperBodyLayer();
        InitializeBashLayer();
        WarnAboutMissingTriggers();
        _context.AttackTriggerHashes = CollectAttackTriggerHashes();

        // Each strategy owns its own input check, so order only sets priority-of-read; bash first by convention.
        _strategies = new IAttackStrategy[]
        {
            new BashStrategy(bashConfig),
            new HeavyAttackStrategy(heavyAttackConfig),
            new LightAttackStrategy(lightAttackConfig),
        };
    }

    private void OnDisable()
    {
        SpineProxyGate.SetSuspended(_spineProxy, this, false);
    }

    /// <summary>
    /// Aborts any in-progress attack and hit-stop and clears buffered input, releasing the attack
    /// lock so control returns immediately (used on respawn). StopAllCoroutines skips the RunAttack
    /// finally, so IsAttacking is cleared here directly.
    /// </summary>
    public void CancelAttack()
    {
        StopAllCoroutines();
        IsAttacking = false;
        _bufferedStrategyIndex = -1;
        if (_hitStopActive)
        {
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;
            _hitStopActive = false;
        }
    }

    private void Update()
    {
        UpdateUpperBodyLayer();
        UpdateBashLayer();

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
            // Always release the lock even if a strategy throws, or combat stays stuck disabled.
            IsAttacking = false;
            CombatLog.Info($"Attack finished: {strategy.GetType().Name}", this);
        }
    }

    private bool ValidateSetup()
    {
        bool ok = true;

        if (lightAttackConfig == null) { Debug.LogError("[Combat] PlayerCombat: 'Light Attack Config' is not assigned in the Inspector.", this); ok = false; }
        if (heavyAttackConfig == null) { Debug.LogError("[Combat] PlayerCombat: 'Heavy Attack Config' is not assigned in the Inspector.", this); ok = false; }
        if (bashConfig == null)        { Debug.LogError("[Combat] PlayerCombat: 'Bash Config' is not assigned in the Inspector.", this); ok = false; }
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
    /// Distinct, valid trigger hashes across the whole moveset, used to clear stale queued triggers
    /// before each swing so spam-clicking can't stack phantom attacks.
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
        Add(bashConfig);

        var result = new int[hashes.Count];
        hashes.CopyTo(result);
        return result;
    }

    private void WarnAboutMissingTriggers()
    {
        // Walk the combo chain (bounded against an accidental cycle) so a bad trigger on a later
        // step is reported at startup, not several swings into a fight.
        LightAttackConfigSO step = lightAttackConfig;
        for (int i = 0; step != null && i < 16; i++)
        {
            ReportTriggerState(step);
            step = step.nextComboStep;
            if (step == lightAttackConfig) break;
        }

        ReportTriggerState(heavyAttackConfig);
        ReportTriggerState(bashConfig);
        ReportBashVariantState();
    }

    // Covers what the shared trigger/state checks miss: the moving-variant state and routing bool.
    private void ReportBashVariantState()
    {
        if (bashConfig == null || _animator == null) return;

        if (!string.IsNullOrEmpty(bashConfig.movingStateName) &&
            !StateExistsOnAnyLayer(Animator.StringToHash(bashConfig.movingStateName)))
        {
            Debug.LogError(
                $"[Combat] Animator has no state '{bashConfig.movingStateName}' on any layer (attack '{bashConfig.name}') — the moving bash will never animate.",
                this);
        }

        if (!string.IsNullOrEmpty(bashConfig.standingBoolParameter) &&
            _context.ValidAnimatorParameters != null &&
            !_context.ValidAnimatorParameters.Contains(bashConfig.StandingBoolHash))
        {
            Debug.LogError(
                $"[Combat] Animator is missing bool '{bashConfig.standingBoolParameter}' required to route the bash's standing/moving states (attack '{bashConfig.name}').",
                this);
        }
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

    private bool StateExistsOnAnyLayer(int stateHash)
    {
        for (int layer = 0; layer < _animator.layerCount; layer++)
            if (_animator.HasState(layer, stateHash)) return true;
        return false;
    }

    private void InitializeUpperBodyLayer()
    {
        if (_animator == null) return;

        _upperBodyLayerIndex = _animator.GetLayerIndex(upperBodyLayerName);
        if (_upperBodyLayerIndex < 0)
        {
            CombatLog.Warn($"Animator has no '{upperBodyLayerName}' layer — upper-body blending disabled.", this);
            return;
        }

        // Controller authors this Override layer at weight 1 (CombatIdle would permanently replace
        // the top half of locomotion); start at 0 and blend up only while attacking.
        _animator.SetLayerWeight(_upperBodyLayerIndex, 0f);
    }

    private void UpdateUpperBodyLayer()
    {
        if (_upperBodyLayerIndex < 0) return;

        // A dodge roll owns the whole body: drop the upper-body override and its post-attack hold so
        // a lingering combat stance can't play over the roll. Resumes when an attack next starts.
        bool rolling = _dodge != null && _dodge.IsRolling;

        if (rolling) _upperBodyHoldTimer = 0f;
        else if (IsAttacking) _upperBodyHoldTimer = upperBodyHoldTime;
        else if (_upperBodyHoldTimer > 0f) _upperBodyHoldTimer -= Time.deltaTime;

        float target = (!rolling && (IsAttacking || _upperBodyHoldTimer > 0f)) ? 1f : 0f;

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

    private void InitializeBashLayer()
    {
        if (_animator == null) return;

        _bashLayerIndex = _animator.GetLayerIndex(bashLayerName);
        if (_bashLayerIndex < 0)
        {
            CombatLog.Warn($"Animator has no '{bashLayerName}' layer — the standing bash will play on its config's fallback layer.", this);
            return;
        }

        // Must idle at weight 0 or this Override layer's Empty state forces the rig to T-pose.
        // Raised only during the standing bash; the moving bash lives on the Upper Body layer.
        _animator.SetLayerWeight(_bashLayerIndex, 0f);
    }

    private void UpdateBashLayer()
    {
        if (_bashLayerIndex < 0 || bashConfig == null) return;

        // Only the standing variant uses this full-body layer; the moving one rides Upper Body.
        // Drop back to 0 by exit so the layer returns to its pass-through Empty state.
        bool showing = bashConfig.StandingVariantSelected
                       && _context.TryGetAttackNormalizedTime(bashConfig, out float t)
                       && t < bashConfig.exitNormalizedTime;
        float target = showing ? 1f : 0f;

        float current = _animator.GetLayerWeight(_bashLayerIndex);
        if (!Mathf.Approximately(current, target))
        {
            current = Mathf.MoveTowards(current, target, upperBodyBlendSpeed * Time.deltaTime);
            _animator.SetLayerWeight(_bashLayerIndex, current);
        }

        // Suspend the SpineProxy while the standing bash owns the rig, else it stomps the clip's
        // spine every LateUpdate. Weight-driven, so it resumes as the layer blends back out.
        if (_spineProxy != null)
            SpineProxyGate.SetSuspended(_spineProxy, this, current >= spineProxySuspendWeight);
    }

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
        // red = light, orange = heavy (full-charge radius), blue = bash.
        Vector3 origin = transform.position + Vector3.up * 0.8f;
        DrawAttackRangeGizmo(lightAttackConfig, Color.red, origin);
        DrawAttackRangeGizmo(heavyAttackConfig, new Color(1f, 0.5f, 0f), origin);
        DrawAttackRangeGizmo(bashConfig, Color.blue, origin);
    }

    private void DrawAttackRangeGizmo(AttackConfigBaseSO config, Color color, Vector3 origin)
    {
        if (config == null) return;
        Gizmos.color = color;
        Gizmos.DrawWireSphere(origin + transform.forward * config.hitOffset, config.hitRadius);
    }
}
