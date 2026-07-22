// Shows a visual while any buff is active on this GameObject's BuffReceiver.
using UnityEngine;

namespace Signal.Combat.Buffs
{
    [RequireComponent(typeof(BuffReceiver))]
    public class BuffIndicator : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Object toggled on while buffed. Leave empty to auto-create a simple tinted orb.")]
        private GameObject indicator;

        [SerializeField]
        [Tooltip("Local offset for the auto-created orb.")]
        private Vector3 autoIndicatorOffset = new Vector3(0f, 2.2f, 0f);

        private BuffReceiver _receiver;
        private Renderer _indicatorRenderer;
        private MaterialPropertyBlock _tintBlock;
        private static readonly int ColorId = Shader.PropertyToID("_BaseColor");

        private void Awake()
        {
            _receiver = GetComponent<BuffReceiver>();
            _tintBlock = new MaterialPropertyBlock();

            if (indicator == null) CreateFallbackIndicator();
            _indicatorRenderer = indicator != null ? indicator.GetComponentInChildren<Renderer>() : null;
            indicator?.SetActive(false);
        }

        private void OnEnable()
        {
            _receiver.BuffApplied += HandleBuffApplied;
            _receiver.BuffExpired += HandleBuffExpired;
        }

        private void OnDisable()
        {
            _receiver.BuffApplied -= HandleBuffApplied;
            _receiver.BuffExpired -= HandleBuffExpired;
        }

        private void HandleBuffApplied(BuffSO buff)
        {
            if (indicator == null) return;

            indicator.SetActive(true);
            if (_indicatorRenderer != null)
            {
                _tintBlock.SetColor(ColorId, buff.indicatorColor);
                _indicatorRenderer.SetPropertyBlock(_tintBlock);
            }
        }

        private void HandleBuffExpired(BuffSO buff)
        {
            if (indicator != null && _receiver.ActiveBuffCount == 0)
                indicator.SetActive(false);
        }

        private void CreateFallbackIndicator()
        {
            indicator = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            indicator.name = "BuffIndicator (auto)";
            Destroy(indicator.GetComponent<Collider>());
            indicator.transform.SetParent(transform, false);
            indicator.transform.localPosition = autoIndicatorOffset;
            indicator.transform.localScale = Vector3.one * 0.3f;
        }
    }
}
