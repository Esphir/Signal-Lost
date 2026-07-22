// Flashes every renderer a solid color for a very short time whenever the sibling IHealth actually takes damage (mitigated-to-zero hits don't flash).
using System.Collections.Generic;
using UnityEngine;
using Signal.Combat.Data;
using Signal.Combat.Interfaces;

namespace Signal.Combat.Feedback
{
    public class DamageFlash : MonoBehaviour
    {
        [SerializeField, Min(0.01f)]
        [Tooltip("How long the flash lasts, in real (unscaled) seconds so hit-stop doesn't stretch it.")]
        private float flashDuration = 0.07f;

        [SerializeField] private Color flashColor = Color.red;

        [SerializeField]
        [Tooltip("Renderers to flash. Leave empty to auto-collect all child Mesh/SkinnedMesh renderers.")]
        private Renderer[] renderers;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private IHealth _health;
        private MaterialPropertyBlock _flashBlock;
        private MaterialPropertyBlock[] _restoreBlocks;
        private float _flashUntil;
        private bool _flashing;

        private void Awake()
        {
            _health = GetComponent<IHealth>();
            if (_health == null)
            {
                Debug.LogWarning($"[Combat] DamageFlash on '{name}' found no IHealth component — it will never trigger.", this);
                enabled = false;
                return;
            }

            if (renderers == null || renderers.Length == 0)
                renderers = CollectChildRenderers();

            if (renderers.Length == 0)
            {
                Debug.LogWarning($"[Combat] DamageFlash on '{name}' found no renderers to flash.", this);
                enabled = false;
                return;
            }

            _flashBlock = new MaterialPropertyBlock();
            _flashBlock.SetColor(BaseColorId, flashColor);
            _flashBlock.SetColor(ColorId, flashColor);

            _restoreBlocks = new MaterialPropertyBlock[renderers.Length];
            for (int i = 0; i < _restoreBlocks.Length; i++)
                _restoreBlocks[i] = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            if (_health != null) _health.Damaged += HandleDamaged;
        }

        private void OnDisable()
        {
            if (_health != null) _health.Damaged -= HandleDamaged;
            if (_flashing) EndFlash();
        }

        private void HandleDamaged(DamageInfo damageInfo)
        {
            _flashUntil = Time.unscaledTime + flashDuration;
            if (_flashing) return;

            _flashing = true;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                renderers[i].GetPropertyBlock(_restoreBlocks[i]);
                renderers[i].SetPropertyBlock(_flashBlock);
            }
        }

        private void Update()
        {
            if (_flashing && Time.unscaledTime >= _flashUntil)
                EndFlash();
        }

        private void EndFlash()
        {
            _flashing = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;

                renderers[i].SetPropertyBlock(_restoreBlocks[i]);
            }
        }

        private Renderer[] CollectChildRenderers()
        {
            var found = new List<Renderer>();
            foreach (Renderer r in GetComponentsInChildren<Renderer>())
                if (r is MeshRenderer || r is SkinnedMeshRenderer)
                    found.Add(r);
            return found.ToArray();
        }
    }
}
