// Base for every boss attack.
using System.Collections;
using UnityEngine;

namespace Signal.Combat.Boss
{
    public abstract class BossAttack : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Shown in logs; also the label the FSM uses to avoid repeating an attack back-to-back.")]
        private string attackName = "Attack";

        [SerializeField, Min(0f)]
        [Tooltip("Extra cooldown (seconds) before this attack can be chosen again. 0 = only the no-repeat rule applies.")]
        private float cooldown = 0f;

        private float _readyAt;

        public string AttackName => attackName;

        public virtual bool CanUse(BossContext ctx) => Time.time >= _readyAt;

        public abstract float WeightAt(float distanceToPlayer, BossContext ctx);

        public IEnumerator Run(BossContext ctx)
        {
            _readyAt = Time.time + cooldown;
            yield return Execute(ctx);
        }

        protected abstract IEnumerator Execute(BossContext ctx);

        protected static IEnumerator Wait(float seconds, BossContext ctx)
        {
            float scale = Mathf.Max(0.1f, ctx.SpeedMultiplier);
            yield return new WaitForSeconds(seconds / scale);
        }
    }
}
