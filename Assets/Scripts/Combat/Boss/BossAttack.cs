using System.Collections;
using UnityEngine;

namespace Signal.Combat.Boss
{
    /// <summary>
    /// Base for every boss attack. Each attack is a self-contained component the boss carries — add a new
    /// subclass to the boss and it enters the rotation automatically, no AI changes. An attack owns its own
    /// telegraph, active window, and any burning ground it leaves; it runs as one coroutine so the sequence
    /// reads top-to-bottom.
    ///
    /// The AI picks attacks by <see cref="WeightAt"/> (distance-weighted) and never repeats the last one
    /// while another is available, so the fight stays varied and readable.
    /// </summary>
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

        /// <summary>Available when off cooldown. Subclasses can add range/summon-cap conditions.</summary>
        public virtual bool CanUse(BossContext ctx) => Time.time >= _readyAt;

        /// <summary>
        /// Selection weight for the current player distance — higher means likelier. Return 0 to opt out at
        /// this range. This is where "prefer flamethrower up close, summons at range" lives, per attack.
        /// </summary>
        public abstract float WeightAt(float distanceToPlayer, BossContext ctx);

        /// <summary>Runs the whole attack: telegraph, active window, recovery of the attack itself.</summary>
        public IEnumerator Run(BossContext ctx)
        {
            _readyAt = Time.time + cooldown; // stamped up front so chained phase-2 attacks still respect it
            yield return Execute(ctx);
        }

        protected abstract IEnumerator Execute(BossContext ctx);

        /// <summary>Wait scaled by the phase speed multiplier — phase 2 shortens every windup uniformly.</summary>
        protected static IEnumerator Wait(float seconds, BossContext ctx)
        {
            float scale = Mathf.Max(0.1f, ctx.SpeedMultiplier);
            yield return new WaitForSeconds(seconds / scale);
        }
    }
}
