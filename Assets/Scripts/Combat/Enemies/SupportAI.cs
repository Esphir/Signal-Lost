using UnityEngine;
using Signal.Combat.Interfaces;

namespace Signal.Combat.Enemies
{
    /// <summary>
    /// Decision-making for the Support enemy: keep an ally between itself and the player (hide
    /// behind the front line), retreating directly when no allies remain. Buffing is handled
    /// independently by <see cref="AllyBuffAbility"/>; movement by <see cref="EnemyMotor"/> —
    /// this class only picks positions.
    ///
    /// It stays solid against its allies. Hiding behind the front line used to be arranged by switching
    /// collision off between the support and everything near it, which stopped it shoving the line
    /// around but let it stand inside other enemies — so it read as clipping through them rather than
    /// sheltering behind them. It now holds a spacing instead: the position it picks is pushed clear of
    /// every nearby ally, and being crowded is itself a reason to move.
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
        [SerializeField, Min(0.5f)]
        [Tooltip("Spacing kept from allies. Big enough that the support shelters behind the line without leaning on it, and leaves room to Bash it clear of the group.")]
        private float supportDistance = 2.5f;

        private EnemyMotor _motor;
        private IStunnable _stunnable; // optional
        private Transform _threat;
        private Collider[] _allyBuffer;
        private int _allyCount;
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
            desired = ApplySeparation(desired);

            // Standing too close to an ally is a reason to move in its own right, even when the cover
            // position is barely different — otherwise the support settles pressed against the line and
            // stays there, which is the look the old collision-ignoring produced.
            bool crowded = IsCrowded();
            if (crowded || (desired - transform.position).sqrMagnitude > repositionThreshold * repositionThreshold)
                _motor.MoveTowards(StepAround(desired));
            else
                _motor.FaceTowards(_threat.position);
        }

        /// <summary>
        /// Bends the route around an ally standing in the way. Steering only the destination isn't enough:
        /// the walk there is a straight line, so a front-liner between here and cover gets leaned on and
        /// shoved along. This returns a waypoint beside the blocker instead, on whichever side it is
        /// already closer to, and is re-evaluated every frame so the detour curves rather than zig-zags.
        /// </summary>
        private Vector3 StepAround(Vector3 destination)
        {
            Vector3 toDestination = destination - transform.position;
            toDestination.y = 0f;

            float travel = toDestination.magnitude;
            if (travel < 0.01f) return destination;
            Vector3 heading = toDestination / travel;

            for (int i = 0; i < _allyCount; i++)
            {
                Collider ally = _allyBuffer[i];
                if (ally == null || ally.transform.root == transform.root) continue;

                Vector3 toAlly = ally.transform.position - transform.position;
                toAlly.y = 0f;

                float along = Vector3.Dot(toAlly, heading);
                if (along <= 0f || along > travel) continue; // behind us, or beyond where we're going

                Vector3 nearestOnPath = transform.position + heading * along;
                Vector3 offset = ally.transform.position - nearestOnPath;
                offset.y = 0f;
                if (offset.magnitude >= supportDistance) continue; // the path already clears it

                Vector3 side = Vector3.Cross(Vector3.up, heading);
                float away = Vector3.Dot(offset, side) > 0f ? -1f : 1f;
                return nearestOnPath + side * (away * supportDistance);
            }

            return destination;
        }

        /// <summary>True when an ally is inside the spacing — the support should step out, not lean on it.</summary>
        private bool IsCrowded()
        {
            for (int i = 0; i < _allyCount; i++)
            {
                Collider ally = _allyBuffer[i];
                if (ally == null || ally.transform.root == transform.root) continue;

                Vector3 away = transform.position - ally.transform.position;
                away.y = 0f;
                if (away.sqrMagnitude < supportDistance * supportDistance) return true;
            }
            return false;
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

        /// <summary>
        /// Pushes a desired position out of any ally within <see cref="supportDistance"/>, so the
        /// support settles just clear of the group instead of inside it.
        /// </summary>
        private Vector3 ApplySeparation(Vector3 desired)
        {
            for (int i = 0; i < _allyCount; i++)
            {
                Collider ally = _allyBuffer[i];
                if (ally == null || ally.transform.root == transform.root) continue;

                Vector3 away = desired - ally.transform.position;
                away.y = 0f;
                float dist = away.magnitude;
                if (dist > 0.001f && dist < supportDistance)
                    desired += away.normalized * (supportDistance - dist);
            }
            return desired;
        }

        /// <summary>Nearest ally that isn't this object and isn't another support (prefer front-liners).</summary>
        private Transform FindBestCoverAlly()
        {
            _allyCount = Physics.OverlapSphereNonAlloc(
                transform.position, allySearchRadius, _allyBuffer, allyMask, QueryTriggerInteraction.Collide);

            Transform best = null, bestSupport = null;
            float bestDist = float.MaxValue, bestSupportDist = float.MaxValue;

            for (int i = 0; i < _allyCount; i++)
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
