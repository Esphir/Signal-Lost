using System.Collections;
using System.Collections.Generic;
using Signal.Combat.Health;
using Signal.Generation;
using Signal.Run;
using Signal.UI;
using UnityEngine;

namespace Signal.Combat.Boss
{
    /// <summary>
    /// The hot-sauce boss brain: a finite-state machine that loops Prowl → Attack → Recovery. It owns no
    /// attack logic — attacks are separate <see cref="BossAttack"/> components it carries, each of which
    /// telegraphs and executes itself — so the fight is a movement puzzle of readable patterns rather than
    /// a wall of damage. Selection is weighted by player distance (a burst for anything in melee, flame
    /// for mid range, arena control at long) and never repeats the last attack while another is available.
    ///
    /// Between attacks it prowls: it hops to a fresh angle on the player at its preferred range while
    /// staying turned toward them, so the player has to keep repositioning too. It only stands still to
    /// telegraph and to recover — standing still is the tell that it's about to hurt you, or that you can
    /// hurt it.
    ///
    /// At half health it flips to phase 2: windups play faster, recovery windows shrink, flames linger, and
    /// attacks sometimes chain — frantic, but every hit still has a telegraph and an escape. Across runs it
    /// ramps too (see ApplyRunScaling), and killing it ends the floor the way the End room would.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(HealthComponent))]
    public sealed class HotSauceBossAI : MonoBehaviour
    {
        public enum BossState { Sleeping, Idle, Attacking, Recovery, Dead }

        [Header("Presentation")]
        [SerializeField]
        [Tooltip("Name shown on the health bar while the fight is on.")]
        private string bossName = "Hot Sauce";

        [SerializeField]
        [Tooltip("Put a health bar along the bottom of the screen for the length of the fight.")]
        private bool showHealthBar = true;

        [Header("Targeting")]
        [SerializeField] private string playerTag = "Player";
        [SerializeField, Min(1f)] private float activationRange = 25f;

        [Header("Arena")]
        [SerializeField, Min(3f)]
        [Tooltip("Radius of the fight area around the boss's start position — used to place summons.")]
        private float arenaRadius = 10f;

        [Header("Pacing")]
        [SerializeField, Min(0f)] private float idleTime = 0.6f;
        [SerializeField, Min(0f)] private float recoveryTime = 1.5f;

        [Header("Movement")]
        [SerializeField, Min(0f)]
        [Tooltip("Seconds spent hopping to a new angle on the player before each attack.")]
        private float prowlTime = 1.6f;

        [SerializeField, Min(1f)]
        [Tooltip("Range the boss tries to hold while prowling — keep it inside the flamethrower's reach.")]
        private float preferredDistance = 6f;

        [SerializeField, Range(0f, 180f)]
        [Tooltip("How far around the player it swings per reposition. Bigger = wider, more aggressive circling.")]
        private float strafeArc = 100f;

        [Header("Phase 2 (at 50% health)")]
        [SerializeField, Min(1f)] private float phase2SpeedMultiplier = 1.4f;
        [SerializeField, Range(0.1f, 1f)] private float phase2RecoveryScale = 0.5f;
        [SerializeField, Min(1f)] private float phase2FlameDuration = 1.25f;
        [SerializeField, Range(0f, 1f)]
        [Tooltip("Odds of skipping recovery and chaining straight into the next attack in phase 2.")]
        private float phase2ChainChance = 0.5f;

        [Header("Run scaling")]
        [SerializeField]
        [Tooltip("Hit harder and attack more often the more bosses the player has already beaten this save.")]
        private bool scaleWithRun = true;

        [SerializeField]
        [Tooltip("Killing this boss finishes the run — it raises the same screen the End room does.")]
        private bool completesRunOnDeath = true;

        [Header("Setup")]
        [SerializeField]
        [Tooltip("Minion prefab handed to the summon attack if it doesn't already have one (your Little GhostPeppers).")]
        private GameObject ghostPepperPrefab;

        public BossState State { get; private set; } = BossState.Sleeping;

        private HealthComponent _health;
        private BossContext _ctx;
        private BossLocomotion _move;
        private readonly List<BossAttack> _attacks = new List<BossAttack>();
        private readonly List<float> _weights = new List<float>();
        private BossAttack _lastAttack;
        private bool _phase2;
        private float _pace = 1f;

        private void Awake() => _health = GetComponent<HealthComponent>();

        private void Start()
        {
            EnsureAttacks();

            _move = GetComponent<BossLocomotion>();
            if (_move == null) _move = gameObject.AddComponent<BossLocomotion>();

            if (completesRunOnDeath && GetComponent<BossVictoryTrigger>() == null)
                gameObject.AddComponent<BossVictoryTrigger>();

            _ctx = new BossContext
            {
                Boss = transform,
                Instigator = gameObject,
                PlayerTag = playerTag,
                ArenaCenter = transform.position,
                ArenaRadius = arenaRadius,
                Anim = GetComponentInChildren<BossSquashStretch>()
            };
            if (_ctx.Anim == null) _ctx.Anim = gameObject.AddComponent<BossSquashStretch>();

            ApplyRunScaling();

            _health.HealthChanged += OnHealthChanged;
            _health.Died += OnDied;
            StartCoroutine(MainLoop());
        }

        /// <summary>
        /// Ramps the fight for how many bosses the player has already beaten: harder hits, and less breathing
        /// room between attacks. Telegraphs are deliberately left alone — a later boss should be relentless,
        /// not unreadable.
        /// </summary>
        private void ApplyRunScaling()
        {
            if (!scaleWithRun) return;

            LevelGenerator generator = FindFirstObjectByType<LevelGenerator>();
            int tier = RunDifficulty.BossTier(RunDifficulty.CurrentRun, generator != null ? generator.BossFloorInterval : 0);

            _ctx.DamageMultiplier = RunDifficulty.BossDamageMultiplier(tier);
            _pace = RunDifficulty.BossPaceMultiplier(tier);

            if (tier > 1)
                CombatLog.Info($"'{name}' is boss #{tier} this save — ×{_ctx.DamageMultiplier:0.00} damage, ×{_pace:0.00} pace.", this);
        }

        private void OnDestroy()
        {
            if (_health != null)
            {
                _health.HealthChanged -= OnHealthChanged;
                _health.Died -= OnDied;
            }
            // The fight can also end by the room going away — but a death is already draining the bar out.
            if (State != BossState.Dead) BossHealthBarUI.Hide();
        }

        // ── FSM ──────────────────────────────────────────────────────────────────

        private IEnumerator MainLoop()
        {
            // Sleep until the player is present and close (room entry / spawn handles the actual trigger).
            State = BossState.Sleeping;
            while (!_ctx.ResolvePlayer() || _ctx.DistanceToPlayer > activationRange)
                yield return new WaitForSeconds(0.25f);

            // Waking is the start of the fight, so that's when the bar belongs on screen — not while the
            // player is still rooms away.
            if (showHealthBar) BossHealthBarUI.Show(_health, bossName);

            while (State != BossState.Dead)
            {
                State = BossState.Idle;
                _ctx.Anim?.Relax();
                yield return Prowl(Paced(prowlTime));  // hop to a new angle on the player
                yield return Hold(Paced(idleTime));    // then square up, so the telegraph starts from a still pose
                if (State == BossState.Dead) yield break;

                BossAttack attack = ChooseAttack();
                if (attack == null) { yield return new WaitForSeconds(0.3f); continue; }

                State = BossState.Attacking;   // each attack telegraphs then fires inside its own Run
                _lastAttack = attack;
                _move.Release();               // the attack owns the boss's pose from here
                yield return attack.Run(_ctx);
                if (State == BossState.Dead) yield break;

                // Recovery is the damage window — it plants itself. Phase 2 shortens it and sometimes skips
                // it to chain.
                State = BossState.Recovery;
                bool chain = _phase2 && Random.value < phase2ChainChance;
                if (!chain)
                    yield return Hold(Paced(recoveryTime * (_phase2 ? phase2RecoveryScale : 1f)));
            }
        }

        /// <summary>Weighted pick by distance; the last attack is excluded while any other is available.</summary>
        private BossAttack ChooseAttack()
        {
            if (!_ctx.ResolvePlayer()) return null;

            float distance = _ctx.DistanceToPlayer;
            int usable = UsableCount(distance);

            _weights.Clear();
            float total = 0f;
            foreach (BossAttack attack in _attacks)
            {
                float w = attack.CanUse(_ctx) ? Mathf.Max(0f, attack.WeightAt(distance, _ctx)) : 0f;
                if (attack == _lastAttack && usable > 1) w = 0f; // no back-to-back unless it's the only choice
                _weights.Add(w);
                total += w;
            }

            if (total <= 0f)
            {
                foreach (BossAttack attack in _attacks)
                    if (attack.CanUse(_ctx) && attack.WeightAt(distance, _ctx) > 0f) return attack;
                return null;
            }

            float roll = Random.value * total;
            for (int i = 0; i < _attacks.Count; i++)
            {
                roll -= _weights[i];
                if (roll <= 0f) return _attacks[i];
            }
            return _attacks[_attacks.Count - 1];
        }

        private int UsableCount(float distance)
        {
            int count = 0;
            foreach (BossAttack attack in _attacks)
                if (attack.CanUse(_ctx) && attack.WeightAt(distance, _ctx) > 0f) count++;
            return count;
        }

        // ── Phase / life ─────────────────────────────────────────────────────────

        private void OnHealthChanged(float current, float max)
        {
            if (!_phase2 && current > 0f && current <= max * 0.5f) EnterPhase2();
        }

        private void EnterPhase2()
        {
            _phase2 = true;
            _ctx.Phase = 2;
            _ctx.SpeedMultiplier = phase2SpeedMultiplier;
            _ctx.FlameDurationMultiplier = phase2FlameDuration;
            if (_move != null) _move.SpeedScale = phase2SpeedMultiplier;
            _ctx.Anim?.Pulse(0.9f);
            CombatLog.Info($"'{name}' entered phase 2 — faster, chained attacks.", this);
        }

        private void OnDied()
        {
            State = BossState.Dead;
            StopAllCoroutines();
            if (_move != null) _move.Release();
            BossHealthBarUI.Dismiss(); // drains out on screen rather than blinking away with the corpse
            // The fight ends on the boss: clear any summoned peppers so nothing lingers.
            foreach (GhostPepperAI minion in FindObjectsByType<GhostPepperAI>(FindObjectsSortMode.None))
                Destroy(minion.gameObject);
        }

        // ── Setup / helpers ────────────────────────────────────────────────────────

        private void EnsureAttacks()
        {
            GetComponents(_attacks);
            if (_attacks.Count == 0)
            {
                gameObject.AddComponent<FlamethrowerAttack>();
                gameObject.AddComponent<BottleSpinAttack>();
                gameObject.AddComponent<SauceBurstAttack>();
                gameObject.AddComponent<GhostPepperSummonAttack>();
                GetComponents(_attacks);
            }

            foreach (BossAttack attack in _attacks)
                if (attack is GhostPepperSummonAttack summon) summon.SetMinionPrefab(ghostPepperPrefab);
        }

        /// <summary>Every between-attack window, tightened by the run's pace multiplier.</summary>
        private float Paced(float seconds) => seconds / Mathf.Max(0.1f, _pace);

        /// <summary>
        /// Stand still and track the player — the telegraph and recovery pose. It won't hand back until the
        /// boss is actually on the ground, so an attack can never start (and pose the boss) mid-hop.
        /// </summary>
        private IEnumerator Hold(float seconds)
        {
            _move.Stop();
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                if (_ctx.Player != null) _move.Face(_ctx.Player.position);
                yield return null;
            }
            while (_move.Airborne)
            {
                if (_ctx.Player != null) _move.Face(_ctx.Player.position);
                yield return null;
            }
        }

        /// <summary>Hop to a fresh angle on the player, still facing them, picking again on arrival.</summary>
        private IEnumerator Prowl(float seconds)
        {
            if (seconds <= 0f || !_ctx.ResolvePlayer()) { yield return Hold(seconds); yield break; }

            _move.MoveTo(PickProwlSpot());
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                if (_ctx.Player != null) _move.Face(_ctx.Player.position);
                if (!_move.HasDestination) _move.MoveTo(PickProwlSpot());
                yield return null;
            }
            _move.Stop();
        }

        /// <summary>A point at the preferred range, swung around the player, kept inside the arena.</summary>
        private Vector3 PickProwlSpot()
        {
            Vector3 player = _ctx.PlayerPosition;
            Vector3 fromPlayer = transform.position - player; fromPlayer.y = 0f;
            if (fromPlayer.sqrMagnitude < 0.01f) fromPlayer = Vector3.forward;

            float arc = strafeArc * Random.Range(0.35f, 1f) * (Random.value < 0.5f ? -1f : 1f);
            Vector3 dir = Quaternion.Euler(0f, arc, 0f) * fromPlayer.normalized;
            Vector3 spot = player + dir * (preferredDistance * Random.Range(0.8f, 1.15f)); // vary so it isn't a fixed orbit

            // Never wander out of the fight: pull the spot back inside the arena ring.
            Vector3 fromCenter = spot - _ctx.ArenaCenter; fromCenter.y = 0f;
            float limit = arenaRadius * 0.8f;
            if (fromCenter.sqrMagnitude > limit * limit)
                spot = _ctx.ArenaCenter + fromCenter.normalized * limit;

            spot.y = transform.position.y;
            return spot;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, arenaRadius);
        }
    }
}
