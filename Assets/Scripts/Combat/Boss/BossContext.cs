using Signal.Combat.Data;
using Signal.Combat.Interfaces;
using UnityEngine;

namespace Signal.Combat.Boss
{
    /// <summary>
    /// Shared state handed to every boss attack: who the boss is, where the player is, the arena, and the
    /// current phase's tuning multipliers. The AI owns one and passes it into each attack's Run, so attacks
    /// stay decoupled from the AI and from each other. Also the single place damage reaches the player, so
    /// no attack hard-codes how to find or hurt them.
    /// </summary>
    public sealed class BossContext
    {
        public Transform Boss;
        public GameObject Instigator;
        public LayerMask HitMask;
        public string PlayerTag = "Player";

        /// <summary>Centre and radius of the fight area — used to place summons and read arena space.</summary>
        public Vector3 ArenaCenter;
        public float ArenaRadius = 10f;

        /// <summary>1 before half health, 2 after. Attacks read this to ramp intensity.</summary>
        public int Phase = 1;

        /// <summary>Above 1 in phase 2: telegraphs/sweeps play faster.</summary>
        public float SpeedMultiplier = 1f;

        /// <summary>Above 1 in phase 2: flames and burning ground last a little longer.</summary>
        public float FlameDurationMultiplier = 1f;

        /// <summary>
        /// Scales every point of damage the boss deals, set once from the run's boss tier. Attacks stay
        /// authored at their run-1 values and this does the ramp, so tuning a number in the inspector still
        /// means what it says.
        /// </summary>
        public float DamageMultiplier = 1f;

        /// <summary>Squash-and-stretch driver, for anticipation/impact cues. May be null.</summary>
        public BossSquashStretch Anim;

        public Transform Player { get; private set; }
        public IDamageable PlayerDamageable { get; private set; }

        /// <summary>Finds/caches the player. False when there's no player to fight.</summary>
        public bool ResolvePlayer()
        {
            if (Player == null)
            {
                GameObject p = GameObject.FindGameObjectWithTag(PlayerTag);
                if (p != null)
                {
                    Player = p.transform;
                    PlayerDamageable = p.GetComponentInChildren<IDamageable>();
                }
            }
            return Player != null;
        }

        public float DistanceToPlayer =>
            Player != null ? Vector3.Distance(Flat(Boss.position), Flat(Player.position)) : Mathf.Infinity;

        public Vector3 PlayerPosition => Player != null ? Player.position : Boss.position + Boss.forward;

        /// <summary>The one path damage takes to the player — respects i-frames and shields via IDamageable.</summary>
        public void DamagePlayer(float amount, Vector3 hitPoint)
        {
            if (amount <= 0f || PlayerDamageable == null || !PlayerDamageable.IsAlive) return;
            Vector3 dir = Player != null ? (Player.position - Boss.position).normalized : Vector3.forward;
            PlayerDamageable.TakeDamage(new DamageInfo(ScaleDamage(amount), Instigator, hitPoint, dir));
        }

        /// <summary>
        /// Applies the run's damage ramp. <see cref="DamagePlayer"/> does this itself; hand-offs that hurt
        /// the player on their own — burning ground, summoned peppers — scale their rates through here.
        /// </summary>
        public float ScaleDamage(float amount) => amount * DamageMultiplier;

        private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
    }
}
