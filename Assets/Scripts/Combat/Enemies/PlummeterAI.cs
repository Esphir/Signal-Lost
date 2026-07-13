using UnityEngine;
using Signal.Combat.Interfaces;

namespace Signal.Combat.Enemies
{
    /// <summary>
    /// Decision-making for the Plummeter: chase the player into slam range, then trigger the
    /// <see cref="SlamAttackAbility"/>. Movement is delegated to <see cref="EnemyMotor"/>
    /// (which already yields to stun/knockback), the attack to the ability component —
    /// this class only decides.
    /// </summary>
    [RequireComponent(typeof(EnemyMotor), typeof(SlamAttackAbility))]
    public class PlummeterAI : MonoBehaviour
    {
        [Header("Targeting")]
        [SerializeField] private string targetTag = "Player";
        [SerializeField, Min(1f)] private float detectionRange = 15f;
        [SerializeField, Min(0.5f)] private float attackRange = 3.5f;

        private EnemyMotor _motor;
        private SlamAttackAbility _slam;
        private IStunnable _stunnable; // optional — stun-immune variants just omit it
        private Transform _target;
        private float _nextTargetSearch;

        private void Awake()
        {
            _motor = GetComponent<EnemyMotor>();
            _slam = GetComponent<SlamAttackAbility>();
            _stunnable = GetComponent<IStunnable>();
        }

        private void Update()
        {
            if (_stunnable != null && _stunnable.IsStunned) { _motor.Stop(); return; }
            if (_slam.IsExecuting) { _motor.Stop(); return; }

            if (!TryAcquireTarget()) { _motor.Stop(); return; }

            float distance = Vector3.Distance(transform.position, _target.position);
            if (distance > detectionRange) { _motor.Stop(); return; }

            if (distance <= attackRange)
            {
                _motor.Stop();
                _motor.FaceTowards(_target.position);
                if (_slam.CooldownReady)
                    _slam.TryExecute(_target.position);
                return;
            }

            _motor.MoveTowards(_target.position);
        }

        private bool TryAcquireTarget()
        {
            if (_target != null) return true;
            if (Time.time < _nextTargetSearch) return false;

            _nextTargetSearch = Time.time + 1f; // cheap periodic re-scan
            GameObject found = GameObject.FindGameObjectWithTag(targetTag);
            if (found != null) _target = found.transform;
            return _target != null;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, detectionRange);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);
        }
    }
}
