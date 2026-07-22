// Shared state handed to every boss attack: who the boss is, where the player is, the arena, and the current phase's tuning multipliers.
using Signal.Combat.Data;
using Signal.Combat.Interfaces;
using UnityEngine;

namespace Signal.Combat.Boss
{
    public sealed class BossContext
    {
        public Transform Boss;
        public GameObject Instigator;
        public LayerMask HitMask;
        public string PlayerTag = "Player";

        public Vector3 ArenaCenter;
        public float ArenaRadius = 10f;

        public int Phase = 1;

        public float SpeedMultiplier = 1f;

        public float FlameDurationMultiplier = 1f;

        public float DamageMultiplier = 1f;

        public BossSquashStretch Anim;

        public Transform Player { get; private set; }
        public IDamageable PlayerDamageable { get; private set; }

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

        public void DamagePlayer(float amount, Vector3 hitPoint)
        {
            if (amount <= 0f || PlayerDamageable == null || !PlayerDamageable.IsAlive) return;
            Vector3 dir = Player != null ? (Player.position - Boss.position).normalized : Vector3.forward;
            PlayerDamageable.TakeDamage(new DamageInfo(ScaleDamage(amount), Instigator, hitPoint, dir));
        }

        public float ScaleDamage(float amount) => amount * DamageMultiplier;

        private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
    }
}
