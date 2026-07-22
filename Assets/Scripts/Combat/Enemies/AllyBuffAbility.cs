// Periodically applies the configured BuffSO to every ally in radius.
using System.Collections.Generic;
using UnityEngine;
using Signal.Combat.Buffs;
using Signal.Combat.Interfaces;

namespace Signal.Combat.Enemies
{
    public class AllyBuffAbility : MonoBehaviour
    {
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
            _buffer = new Collider[maxAllies * 2];

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

            _nextCastTime = Time.time + (buffedCount > 0 ? cooldown : 0.5f);

            if (buffedCount > 0)
            {
                CombatLog.Info($"'{name}' buffed {buffedCount} ally(ies) with '{buff.name}'.", this);
                BuffCast?.Invoke(buffedCount);
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
                if (!_buffedThisCast.Add(buffable)) continue;

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
