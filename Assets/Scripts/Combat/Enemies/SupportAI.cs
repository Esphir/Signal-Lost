using UnityEngine;
using Signal.Combat.Interfaces;

namespace Signal.Combat.Enemies
{
    /// <summary>
    /// Decision-making for the Support enemy: keep an ally between itself and the player (hide
    /// behind the front line), retreating directly when no allies remain. Buffing is handled
    /// independently by <see cref="AllyBuffAbility"/>; movement by <see cref="EnemyMotor"/> —
    /// this class only picks positions.
    /// </summary>
    [RequireComponent(typeof(EnemyMotor), typeof(AllyBuffAbility))]
    public class SupportAI : MonoBehaviour
    {
        [Header("Targeting")]
        [SerializeField] private string threatTag = "Player";
        [SerializeField] private string allyTag = "Enemy";
        [SerializeField, Min(1f)] private float allySearchRadius = 12f;
        [SerializeField]
        [Tooltip("Layers allies live on (e.g. the enemy hit-mask layer).")]
        private LayerMask allyMask;

        [Header("Positioning")]
        [SerializeField, Min(0.5f)]
        [Tooltip("How far behind the covering ally (relative to the player) to stand.")]
        private float coverDistance = 2.5f;
        [SerializeField, Min(1f)]
        [Tooltip("Minimum distance kept from the player when no allies are left to hide behind.")]
        private float retreatDistance = 8f;
        [SerializeField, Min(0.1f)]
        [Tooltip("Don't reposition for corrections smaller than this (prevents jittering).")]
        private float repositionThreshold = 0.75f;

        private EnemyMotor _motor;
        private IStunnable _stunnable; // optional
        private Transform _threat;
        private Collider[] _allyBuffer;
        private float _nextThreatSearch;

        private void Awake()
        {
            _motor = GetComponent<EnemyMotor>();
            _stunnable = GetComponent<IStunnable>();
            _allyBuffer = new Collider[16];
        }

        private void Update()
        {
            if (_stunnable != null && _stunnable.IsStunned) { _motor.Stop(); return; }
            if (!TryAcquireThreat()) { _motor.Stop(); return; }

            Transform cover = FindBestCoverAlly();
            Vector3 desired = cover != null
                ? CoverPositionBehind(cover)
                : RetreatPosition();

            if ((desired - transform.position).sqrMagnitude > repositionThreshold * repositionThreshold)
                _motor.MoveTowards(desired);
            else
                _motor.FaceTowards(_threat.position);
        }

        /// <summary>Point behind the ally on the line away from the player.</summary>
        private Vector3 CoverPositionBehind(Transform ally)
        {
            Vector3 awayFromThreat = ally.position - _threat.position;
            awayFromThreat.y = 0f;
            if (awayFromThreat.sqrMagnitude < 0.01f) awayFromThreat = -transform.forward;
            return ally.position + awayFromThreat.normalized * coverDistance;
        }

        /// <summary>With no allies left: back straight away from the player to the safe distance.</summary>
        private Vector3 RetreatPosition()
        {
            Vector3 fromThreat = transform.position - _threat.position;
            fromThreat.y = 0f;
            if (fromThreat.magnitude >= retreatDistance) return transform.position; // already safe
            if (fromThreat.sqrMagnitude < 0.01f) fromThreat = transform.forward;
            return _threat.position + fromThreat.normalized * retreatDistance;
        }

        /// <summary>Nearest ally that isn't this object and isn't another support (prefer front-liners).</summary>
        private Transform FindBestCoverAlly()
        {
            int count = Physics.OverlapSphereNonAlloc(
                transform.position, allySearchRadius, _allyBuffer, allyMask, QueryTriggerInteraction.Collide);

            Transform best = null, bestSupport = null;
            float bestDist = float.MaxValue, bestSupportDist = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                Transform root = _allyBuffer[i].transform.root;
                if (root == transform.root) continue;
                if (!root.CompareTag(allyTag)) continue;

                float dist = (root.position - transform.position).sqrMagnitude;
                if (root.GetComponentInChildren<SupportAI>() != null)
                {
                    if (dist < bestSupportDist) { bestSupportDist = dist; bestSupport = root; }
                }
                else if (dist < bestDist)
                {
                    bestDist = dist;
                    best = root;
                }
            }

            return best != null ? best : bestSupport; // hide behind another support only as a last resort
        }

        private bool TryAcquireThreat()
        {
            if (_threat != null) return true;
            if (Time.time < _nextThreatSearch) return false;

            _nextThreatSearch = Time.time + 1f;
            GameObject found = GameObject.FindGameObjectWithTag(threatTag);
            if (found != null) _threat = found.transform;
            return _threat != null;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, allySearchRadius);
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(transform.position, retreatDistance);
        }
    }
}
