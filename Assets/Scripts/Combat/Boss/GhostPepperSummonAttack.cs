// Attack 4 — Ghost Pepper Summons.
using System.Collections;
using System.Collections.Generic;
using Signal.Combat.Enemies;
using Signal.Combat.Telegraphs;
using Signal.Spawning;
using UnityEngine;

namespace Signal.Combat.Boss
{
    public sealed class GhostPepperSummonAttack : BossAttack
    {
        [Header("Summon")]
        [SerializeField] private GameObject minionPrefab;
        [SerializeField, Min(1)] private int countPerCast = 2;
        [SerializeField, Min(1)] private int maxAlive = 3;
        [SerializeField, Min(0.3f)] private float telegraph = 0.9f;

        [SerializeField, Min(0f)]
        [Tooltip("Seconds before the boss will summon again — the main brake on pepper spam.")]
        private float resummonDelay = 14f;

        [Header("Minion tuning")]
        [SerializeField, Min(1f)] private float minionHealth = 20f;
        [SerializeField, Min(0f)] private float minionContactDps = 12f;
        [SerializeField, Min(1f)] private float minionMoveSpeed = 4.5f;

        [Header("Placement")]
        [SerializeField, Range(0.5f, 1f)] private float edgeFraction = 0.85f;
        [SerializeField] private Color telegraphColor = new Color(1f, 0.3f, 0.2f, 1f);

        private readonly List<GhostPepperAI> _alive = new List<GhostPepperAI>();
        private readonly List<AoeTelegraph> _markers = new List<AoeTelegraph>();
        private float _nextSummonAt;

        public int AliveCount { get { Prune(); return _alive.Count; } }

        private void OnEnable() => _nextSummonAt = Time.time + resummonDelay * 0.5f;

        public void SetMinionPrefab(GameObject prefab)
        {
            if (minionPrefab == null) minionPrefab = prefab;
        }

        public override bool CanUse(BossContext ctx)
            => base.CanUse(ctx) && Time.time >= _nextSummonAt && minionPrefab != null
               && AliveCount + countPerCast <= maxAlive;

        public override float WeightAt(float distance, BossContext ctx)
        {
            return distance > ctx.ArenaRadius * 0.45f ? 1.6f : 0.5f;
        }

        protected override IEnumerator Execute(BossContext ctx)
        {
            Prune();
            int count = Mathf.Min(countPerCast, maxAlive - _alive.Count);
            if (count <= 0) yield break;

            var spots = new Vector3[count];
            float baseAngle = Random.value * 360f;
            for (int i = 0; i < count; i++)
            {
                float ang = (baseAngle + i * (360f / count)) * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                spots[i] = ctx.ArenaCenter + dir * (ctx.ArenaRadius * edgeFraction);
                spots[i].y = ctx.Boss.position.y;
                ShowMarker(spots[i], telegraph / Mathf.Max(0.1f, ctx.SpeedMultiplier));
            }

            ctx.Anim?.Anticipate(-0.3f);
            yield return Wait(telegraph, ctx);
            ClearMarkers();
            ctx.Anim?.Pulse(0.5f);

            foreach (Vector3 spot in spots)
            {
                GhostPepperAI minion = Spawn(spot, ctx);
                if (minion != null) _alive.Add(minion);
            }
            _nextSummonAt = Time.time + resummonDelay;
            ctx.Anim?.Relax();
        }

        private GhostPepperAI Spawn(Vector3 position, BossContext ctx)
        {
            GameObject go = Instantiate(minionPrefab, position, Quaternion.identity);
            go.tag = ctx.Boss.CompareTag("Enemy") ? "Enemy" : go.tag;
            go.layer = ctx.Boss.gameObject.layer;

            EnemySafetyNets.Attach(go, position, null);

            var ai = go.GetComponent<GhostPepperAI>();
            if (ai == null) ai = go.AddComponent<GhostPepperAI>();
            ai.Configure(ctx.Boss, minionHealth, ctx.ScaleDamage(minionContactDps), minionMoveSpeed);
            return ai;
        }

        private void Prune() => _alive.RemoveAll(m => m == null);

        private void ShowMarker(Vector3 pos, float duration)
        {
            var marker = AoeTelegraph.Create(null);
            marker.Show(pos, new AoeTelegraphSettings
            {
                Radius = 1.2f,
                Color = telegraphColor,
                ScaleMultiplier = 1f,
                PulseSpeed = 3f,
                WarningDuration = duration
            });
            _markers.Add(marker);
        }

        private void ClearMarkers()
        {
            foreach (AoeTelegraph m in _markers) if (m != null) Destroy(m.gameObject);
            _markers.Clear();
        }

        private void OnDestroy() => ClearMarkers();
    }
}
