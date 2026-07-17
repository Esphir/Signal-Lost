using System.Collections.Generic;
using UnityEngine;
using Signal.Combat.Buffs;
using Signal.Combat.Interfaces;

namespace Signal.Combat.Enemies
{
    /// <summary>
    /// Periodically applies the configured <see cref="BuffSO"/> to every ally in radius. Which
    /// buff is cast is pure data — swap the asset to change behavior, add new BuffSO subclasses
    /// for new buff types; this component never changes. Never buffs its own GameObject.
    /// </summary>
    public class AllyBuffAbility : MonoBehaviour
    {
        /// <summary>
        /// Raised when a cast actually lands on at least one ally, with how many were buffed.
        /// Audio/VFX listen; this ability depends on neither.
        /// </summary>
        public event System.Action<int> BuffCast;

        [Header("Buff")]
        [SerializeField]
        [Tooltip("The buff cast on allies. Any BuffSO subclass works (shield, damage reduction, future types).")]
        private BuffSO buff;
        [SerializeField, Min(0.5f)] private float cooldown = 6f;

        [Header("Targeting")]
        [SerializeField, Min(0.5f)] private float buffRadius = 8f;
        [SerializeField]
        [Tooltip("Layers allies live on (e.g. the enemy hit-mask layer).")]
        private LayerMask allyMask;
        [SerializeField, Min(1)] private int maxAllies = 8;

        private Collider[] _buffer;
        private readonly HashSet<IBuffable> _buffedThisCast = new HashSet<IBuffable>();
        private float _nextCastTime;

        private void Awake()
        {
            _buffer = new Collider[maxAllies * 2]; // allies may have several colliders each

            if (buff == null)
            {
                Debug.LogError($"[Combat] AllyBuffAbility on '{name}' has no BuffSO assigned.", this);
                enabled = false;
            }
        }

        private void Update()
        {
            if (Time.time < _nextCastTime) return;

            int buffedCount = CastOnNearbyAllies();
            // Full cooldown only when the cast did something; otherwise retry again soon.
            _nextCastTime = Time.time + (buffedCount > 0 ? cooldown : 0.5f);

            if (buffedCount > 0)
            {
                CombatLog.Info($"'{name}' buffed {buffedCount} ally(ies) with '{buff.name}'.", this);
                BuffCast?.Invoke(buffedCount); // notification only — no audio/VFX knowledge here
            }
        }

        private int CastOnNearbyAllies()
        {
            int count = Physics.OverlapSphereNonAlloc(
                transform.position, buffRadius, _buffer, allyMask, QueryTriggerInteraction.Collide);

            _buffedThisCast.Clear();
            int buffed = 0;

            for (int i = 0; i < count; i++)
            {
                var buffable = _buffer[i].GetComponentInParent<IBuffable>();
                if (buffable == null) continue;
                if (!_buffedThisCast.Add(buffable)) continue; // multi-collider dedup

                // Never buff ourselves.
                var buffableComponent = buffable as Component;
                if (buffableComponent != null && buffableComponent.transform.root == transform.root) continue;

                if (buffable.ApplyBuff(buff)) buffed++;
                if (buffed >= maxAllies) break;
            }

            return buffed;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, buffRadius);
        }
    }
}
