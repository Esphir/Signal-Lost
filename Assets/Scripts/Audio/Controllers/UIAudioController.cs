using Signal.Run;
using UnityEngine;
using UnityEngine.UI;

namespace Signal.Audio
{
    /// <summary>
    /// Menu feedback. Cues here are 2D (Spatial Blend 0 on the asset), so they ignore the listener's
    /// position. Every button in the game is built by <see cref="Signal.UI.UiBuilder"/>, which attaches
    /// a <see cref="ButtonAudioHooks"/>; those hooks call into this controller, so hover/click work
    /// across the pause menu, main menu, settings, run-end and dev menu without any of them changing.
    /// </summary>
    public class UIAudioController : AudioControllerBase, IUIAudio
    {
        public static UIAudioController Instance { get; private set; }

        [Header("Buttons")]
        [SerializeField] private AudioCue hover;
        [SerializeField] private AudioCue click;
        [SerializeField] private AudioCue confirm;
        [SerializeField] private AudioCue cancel;
        [SerializeField] private AudioCue error;

        [Header("Menus")]
        [SerializeField] private AudioCue pause;
        [SerializeField] private AudioCue resume;
        [SerializeField] private AudioCue menuOpen;
        [SerializeField] private AudioCue menuClose;

        [Header("Loot")]
        [SerializeField]
        [Tooltip("Played when the player picks loot up (RunManager reports the collection).")]
        private AudioCue lootPickup;

        [SerializeField]
        [Tooltip("Played when an upgrade/stat is chosen from the selection screen.")]
        private AudioCue statSelected;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        private void Start()
        {
            // RunManager is a lazily-created singleton; by Start it's safe to touch.
            RunManager.Instance.LootCollected += OnLootCollected;
            RunManager.Instance.UpgradeAcquired += OnUpgradeAcquired;
        }

        private void OnDisable()
        {
            if (!RunManager.HasInstance) return;
            RunManager.Instance.LootCollected -= OnLootCollected;
            RunManager.Instance.UpgradeAcquired -= OnUpgradeAcquired;
        }

        private void OnLootCollected(ItemRarity rarity) => Play(lootPickup);
        private void OnUpgradeAcquired(RunUpgrade upgrade) => Play(statSelected);

        // ── IUIAudio ──────────────────────────────────────────────────────────

        public void PlayHover() => Play(hover);
        public void PlayClick() => Play(click);
        public void PlayConfirm() => Play(confirm);
        public void PlayCancel() => Play(cancel);
        public void PlayError() => Play(error);

        // ── Menu transitions (call from UnityEvents or menu scripts) ──────────

        public void PlayPause() => Play(pause);
        public void PlayResume() => Play(resume);
        public void PlayMenuOpen() => Play(menuOpen);
        public void PlayMenuClose() => Play(menuClose);

        /// <summary>Wires hover/click audio onto a button built outside UiBuilder.</summary>
        public static void Bind(Button button)
        {
            if (button != null && button.GetComponent<ButtonAudioHooks>() == null)
                button.gameObject.AddComponent<ButtonAudioHooks>();
        }
    }
}
