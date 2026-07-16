using Signal.Combat.Interfaces;
using Signal.World;
using UnityEngine;

namespace Signal.Combat.Stun
{
    /// <summary>
    /// Generic stun feedback for any enemy: while its <see cref="IStunnable"/> is stunned it shows a
    /// pooled billboard <see cref="StunIndicator"/> above the head and plays a pooled, looping
    /// <c>StunVFX</c>; when the stun ends (or the enemy is disabled/destroyed) it removes both. Just
    /// attach this next to an IStunnable — no per-enemy code — so every current and future enemy gets
    /// stun visuals for free.
    /// </summary>
    public class StunVisuals : MonoBehaviour
    {
        [Header("VFX")]
        [SerializeField]
        [Tooltip("Pooled StunVFX prefab played (looping) for the stun's duration.")]
        private GameObject stunVfxPrefab;
        [SerializeField]
        [Tooltip("Local offset (above the head) where the swirling VFX plays.")]
        private Vector3 vfxOffset = new Vector3(0f, 1.9f, 0f);

        [Header("Indicator")]
        [SerializeField]
        [Tooltip("World-space offset above the enemy for the stun icon.")]
        private Vector3 indicatorOffset = new Vector3(0f, 2.4f, 0f);
        [SerializeField]
        [Tooltip("Glyph shown in the stun icon.")]
        private string indicatorGlyph = "★"; // ★
        [SerializeField] private Color indicatorColor = new Color(1f, 0.86f, 0.2f);
        [SerializeField]
        [Tooltip("Camera the icon faces. Empty = Camera.main.")]
        private Camera billboardCamera;

        private IStunnable _stunnable;
        private StunIndicator _indicator;
        private PooledVfx _vfx;

        private void Awake() => _stunnable = GetComponent<IStunnable>();

        private void OnEnable()
        {
            if (_stunnable == null) return;
            _stunnable.StunStarted += HandleStunStarted;
            _stunnable.StunEnded += HandleStunEnded;
        }

        private void OnDisable()
        {
            if (_stunnable != null)
            {
                _stunnable.StunStarted -= HandleStunStarted;
                _stunnable.StunEnded -= HandleStunEnded;
            }
            HandleStunEnded(); // never leave a pooled indicator/VFX orphaned on despawn
        }

        private void HandleStunStarted()
        {
            // Indicator: pooled world-space billboard following the head.
            if (_indicator == null)
            {
                _indicator = StunIndicatorPool.Get();
                _indicator.Show(transform, indicatorOffset, billboardCamera, indicatorGlyph, indicatorColor);
            }

            // VFX: pooled, looping, parented so it tracks the enemy — stopped explicitly on stun end.
            if (_vfx == null && stunVfxPrefab != null)
            {
                _vfx = VfxPool.Play(stunVfxPrefab, transform.position + vfxOffset, Quaternion.identity);
                if (_vfx != null)
                {
                    _vfx.transform.SetParent(transform, worldPositionStays: true);
                    _vfx.transform.localPosition = vfxOffset;
                    _vfx.PlaySustained();
                }
            }
        }

        private void HandleStunEnded()
        {
            if (_indicator != null) { _indicator.Hide(); _indicator = null; }
            if (_vfx != null) { _vfx.StopAndRelease(); _vfx = null; }
        }
    }
}
