// A dropped loot instance.
using Signal.Run;
using Signal.Run.Upgrades;
using UnityEngine;

namespace Signal.Loot
{
    [RequireComponent(typeof(Rigidbody))]
    public class LootPickup : MonoBehaviour
    {
        [Header("Pickup")]
        [SerializeField, Min(0.1f)]
        [Tooltip("World-space radius of the pickup trigger.")]
        private float pickupRadius = 1.5f;

        [Header("Drop & Idle Animation")]
        [SerializeField, Min(0f)]
        [Tooltip("Upward/outward impulse when spawned, so loot pops out of the enemy.")]
        private float popImpulse = 3f;
        [SerializeField, Min(0f)]
        [Tooltip("Seconds of free physics before the loot locks in place and starts bobbing.")]
        private float settleTime = 0.8f;
        [SerializeField] private float spinSpeed = 120f;
        [SerializeField] private float bobAmplitude = 0.12f;
        [SerializeField] private float bobFrequency = 2f;

        public ItemRarity Rarity { get; private set; }

        internal LootPickup PoolPrefab;

        private LootSettingsSO _settings;
        private Rigidbody _rigidbody;
        private Renderer _renderer;
        private float _age;
        private bool _settled;
        private float _bobBaseY;
        private float _bobTime;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _renderer = GetComponentInChildren<Renderer>();

            var trigger = gameObject.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            float scale = Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z);
            trigger.radius = pickupRadius / Mathf.Max(0.01f, scale);
        }

        public void Initialize(ItemRarity rarity, LootSettingsSO settings)
        {
            Rarity = rarity;
            _settings = settings;
            _age = 0f;
            _settled = false;
            _bobTime = 0f;

            Material material = settings != null ? settings.GetMaterial(rarity) : null;
            if (material != null && _renderer != null) _renderer.sharedMaterial = material;

            _rigidbody.isKinematic = false;
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;

            Vector2 sideways = Random.insideUnitCircle * 0.4f;
            _rigidbody.AddForce(new Vector3(sideways.x, 1f, sideways.y) * popImpulse, ForceMode.Impulse);
        }

        private void Update()
        {
            if (!_settled)
            {
                _age += Time.deltaTime;
                if (_age >= settleTime && _rigidbody.linearVelocity.sqrMagnitude < 0.1f)
                {
                    _settled = true;
                    _rigidbody.isKinematic = true;
                    _bobBaseY = transform.position.y;
                }
                return;
            }

            _bobTime += Time.deltaTime;
            transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
            Vector3 position = transform.position;
            position.y = _bobBaseY + Mathf.Sin(_bobTime * bobFrequency * Mathf.PI * 2f) * bobAmplitude + bobAmplitude;
            transform.position = position;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;

            UpgradeSelectionUI ui = other.GetComponentInParent<UpgradeSelectionUI>();
            if (ui == null) ui = FindFirstObjectByType<UpgradeSelectionUI>();
            if (ui == null)
            {
                Debug.LogWarning("[Loot] No UpgradeSelectionUI found — loot cannot be picked up.", this);
                return;
            }
            if (ui.IsOpen) return;

            SpawnPickupVfx();
            if (RunManager.HasInstance) RunManager.Instance.ReportLootCollected(Rarity);
            ui.Open(Rarity, _settings != null ? _settings.GetColor(Rarity) : Color.white);
            LootPool.Release(this);
        }

        private void SpawnPickupVfx()
        {
            var go = new GameObject("LootPickupVFX");
            go.transform.position = transform.position;

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = ps.main;
            main.duration = 0.5f;
            main.loop = false;
            main.startLifetime = 0.4f;
            main.startSpeed = 3f;
            main.startSize = 0.08f;
            main.startColor = _settings != null ? _settings.GetColor(Rarity) : Color.white;
            main.stopAction = ParticleSystemStopAction.Destroy;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 24) });

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.15f;

            if (_renderer != null)
                go.GetComponent<ParticleSystemRenderer>().sharedMaterial = _renderer.sharedMaterial;

            ps.Play();
        }
    }
}
