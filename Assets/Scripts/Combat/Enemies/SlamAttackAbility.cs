using System;
using System.Collections;
using UnityEngine;
using Signal.Combat.Detection;
using Signal.Combat.Telegraphs;

namespace Signal.Combat.Enemies
{
    /// <summary>
    /// The Plummeter's signature move: leap above the target point, hang for anticipation, crash
    /// down, and emit an expanding AoE shockwave that damages each target exactly once as the
    /// front reaches them. Pure ability component — the AI decides *when*, this executes *how*.
    /// Reuses the shared hit detector/resolver, so damage flows through IDamageable like every
    /// other attack in the game.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class SlamAttackAbility : MonoBehaviour
    {
        [SerializeField] private SlamAttackConfigSO config;
        [SerializeField]
        [Tooltip("Layers the shockwave can damage (typically the player's layer).")]
        private LayerMask hitMask;

        /// <summary>True from leap start until the shockwave finishes expanding.</summary>
        public bool IsExecuting { get; private set; }
        public bool CooldownReady => !IsExecuting && Time.time >= _nextReadyTime;

        /// <summary>Raised at ground impact with the impact point — hook extra VFX/camera shake here.</summary>
        public event Action<Vector3> Impacted;

        private Rigidbody _rb;
        private OverlapSphereHitDetector _detector;
        private readonly CombatHitResolver _resolver = new CombatHitResolver();
        private AoeTelegraph _telegraph; // created once, reused per slam
        private float _nextReadyTime;
        private float _liveShockwaveRadius; // for gizmos
        private Vector3 _liveShockwaveCenter;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            if (config == null)
            {
                Debug.LogError($"[Combat] SlamAttackAbility on '{name}' has no config assigned.", this);
                enabled = false;
                return;
            }
            _detector = new OverlapSphereHitDetector(config.maxTargets);
        }

        /// <summary>Starts the slam toward the target's position at call time. False if on cooldown.</summary>
        public bool TryExecute(Vector3 targetPoint)
        {
            if (!CooldownReady || !enabled) return false;
            StartCoroutine(Run(targetPoint));
            return true;
        }

        private IEnumerator Run(Vector3 targetPoint)
        {
            IsExecuting = true;
            _nextReadyTime = Time.time + config.cooldown;
            CombatLog.Info($"'{name}' slam attack started (target {targetPoint}).", this);

            // Scripted motion: physics is suspended for the leap so nothing fights the arc.
            bool wasKinematic = _rb.isKinematic;
            _rb.isKinematic = true;

            Vector3 start = transform.position;
            // Land at the target's ground position; keep our own height as ground reference
            // (flat-arena assumption — swap in a ground raycast here for uneven terrain).
            // The landing point is locked HERE, when the leap commits — it never changes after
            // the warning appears, so the telegraph is always truthful.
            Vector3 landing = new Vector3(targetPoint.x, start.y, targetPoint.z);
            Vector3 apex = landing + Vector3.up * config.jumpHeight;

            ShowTelegraph(landing);

            if (config.preJumpDelay > 0f) yield return new WaitForSeconds(config.preJumpDelay);

            // Rise: smooth ease up and over the target.
            for (float t = 0f; t < config.riseDuration; t += Time.deltaTime)
            {
                transform.position = Vector3.Lerp(start, apex, Mathf.SmoothStep(0f, 1f, t / config.riseDuration));
                yield return null;
            }
            transform.position = apex;

            // Anticipation hang at the apex.
            if (config.apexPause > 0f) yield return new WaitForSeconds(config.apexPause);

            // Plummet: ease-in so it accelerates downward.
            for (float t = 0f; t < config.slamDuration; t += Time.deltaTime)
            {
                float k = t / config.slamDuration;
                transform.position = Vector3.Lerp(apex, landing, k * k);
                yield return null;
            }
            transform.position = landing;
            _rb.isKinematic = wasKinematic;

            OnImpact(landing);
            yield return ExpandShockwave(landing);

            IsExecuting = false;
        }

        private void OnImpact(Vector3 point)
        {
            // The warning must vanish the instant the slam lands.
            if (_telegraph != null) _telegraph.Hide();

            if (config.impactVfxPrefab != null)
                Instantiate(config.impactVfxPrefab, point, Quaternion.identity);
            if (config.impactSfx != null)
                AudioSource.PlayClipAtPoint(config.impactSfx, point, config.sfxVolume);

            Impacted?.Invoke(point);
            CombatLog.Info($"'{name}' slammed down at {point} — shockwave expanding.", this);
        }

        /// <summary>
        /// Grows the damage radius from 0 to max at the configured speed, sweeping an overlap
        /// sphere each frame. The resolver's per-swing dedup guarantees each target is damaged
        /// exactly once — the moment the front first reaches it.
        /// </summary>
        private IEnumerator ExpandShockwave(Vector3 center)
        {
            _resolver.BeginSwing();
            _liveShockwaveCenter = center + Vector3.up * 0.5f;
            float radius = 0f;
            int totalHits = 0;

            while (radius < config.aoeMaxRadius)
            {
                radius = Mathf.Min(radius + config.expansionSpeed * Time.deltaTime, config.aoeMaxRadius);
                _liveShockwaveRadius = radius;

                int count = _detector.Detect(_liveShockwaveCenter, radius, hitMask);
                totalHits += _resolver.ApplyDamage(_detector.Buffer, count, config.damage, gameObject, center);

                yield return null;
            }

            CombatLog.Info($"'{name}' shockwave finished: {totalHits} target(s) damaged for {config.damage:0.#}.", this);
            _liveShockwaveRadius = 0f;
        }

        private void ShowTelegraph(Vector3 landing)
        {
            if (!config.showTelegraph) return;

            if (_telegraph == null)
                _telegraph = AoeTelegraph.Create(config.telegraphPrefab);

            // Warning duration = the leap's full committed timeline, so the heat-up tint peaks
            // exactly at impact: pre-jump + rise + apex hang + plummet.
            float warningDuration =
                config.preJumpDelay + config.riseDuration + config.apexPause + config.slamDuration;

            _telegraph.Show(landing, new AoeTelegraphSettings
            {
                Radius = config.aoeMaxRadius,
                Color = config.telegraphColor,
                ScaleMultiplier = config.telegraphScaleMultiplier,
                PulseSpeed = config.telegraphPulseSpeed,
                WarningDuration = warningDuration,
                AppearVfx = config.telegraphVfx,
                AppearSfx = config.telegraphSfx,
                SfxVolume = config.telegraphSfxVolume
            });
        }

        private void OnDisable()
        {
            // Coroutines die with the component — never leave a live warning behind.
            if (_telegraph != null) _telegraph.Hide();
        }

        private void OnDestroy()
        {
            if (_telegraph != null) Destroy(_telegraph.gameObject);
        }

        private void OnDrawGizmosSelected()
        {
            if (config == null) return;

            Gizmos.color = new Color(1f, 0.4f, 0f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, config.aoeMaxRadius);

            if (Application.isPlaying && _liveShockwaveRadius > 0f)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(_liveShockwaveCenter, _liveShockwaveRadius);
            }
        }
    }
}
