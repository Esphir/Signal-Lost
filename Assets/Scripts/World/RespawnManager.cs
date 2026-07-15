using System;
using System.Collections;
using Signal.UI;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Signal.World
{
    /// <summary>
    /// Owns respawning for the current scene: the active checkpoint, the default spawn fallback, and
    /// the actual teleport + state reset. Systems (hazards) request a respawn through
    /// <see cref="TryRespawn"/>; it prevents overlapping respawns and notifies listeners via
    /// <see cref="PlayerRespawned"/>. One per scene (instantiated by the systems bootstrap).
    /// </summary>
    public sealed class RespawnManager : MonoBehaviour
    {
        [Header("Spawn")]
        [SerializeField]
        [Tooltip("Fallback spawn used until a checkpoint is activated. Empty = the player's starting pose, captured at scene start.")]
        private Transform defaultSpawnPoint;
        [SerializeField]
        [Tooltip("Keep the player's current facing instead of snapping to the checkpoint's rotation.")]
        private bool preserveFacing = true;

        [Header("Respawn")]
        [SerializeField]
        [Tooltip("Snap the camera to the new position (no lerp) after a respawn.")]
        private bool cameraReset = true;

        [Header("Respawn VFX")]
        [SerializeField]
        [Tooltip("Play a VFX at the respawn position (pooled). A checkpoint may override the prefab.")]
        private bool playRespawnVfx = true;
        [SerializeField]
        [Tooltip("Default VFX spawned at the respawn position after teleporting.")]
        private GameObject respawnVfxPrefab;

        public static RespawnManager Instance { get; private set; }

        /// <summary>Raised after the player has been repositioned and reset.</summary>
        public event Action<GameObject> PlayerRespawned;

        public Checkpoint ActiveCheckpoint { get; private set; }
        public bool IsRespawning { get; private set; }

        /// <summary>True during the post-respawn grace window; hazards ignore the player while it holds.</summary>
        public bool InRespawnGrace => Time.time < _graceUntil;

        /// <summary>True when a fresh respawn may begin hazards check this before firing splash + respawn.</summary>
        public bool CanRespawn => !IsRespawning && !InRespawnGrace;

        private GameObject _player;
        private Vector3 _defaultPosition;
        private Quaternion _defaultRotation;
        private bool _defaultCaptured;
        private float _graceUntil;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start() => CaptureDefaultSpawn();

        /// <summary>Makes <paramref name="checkpoint"/> the active spawn, deactivating the previous one.</summary>
        public void SetActiveCheckpoint(Checkpoint checkpoint)
        {
            if (checkpoint == null || checkpoint == ActiveCheckpoint) return;

            Checkpoint previous = ActiveCheckpoint;
            ActiveCheckpoint = checkpoint;
            if (previous != null) previous.Deactivate();
            checkpoint.Activate();
        }

        public void ClearCheckpointIfCurrent(Checkpoint checkpoint)
        {
            if (ActiveCheckpoint == checkpoint) ActiveCheckpoint = null;
        }

        /// <summary>
        /// Requests a respawn after <paramref name="delay"/> seconds. Returns false (and does nothing)
        /// if a respawn is already running or the grace window is active, so hazards can't stack
        /// respawns. <paramref name="afterRespawn"/> runs once the player is repositioned (hazards
        /// apply their damage there); <paramref name="graceTime"/> keeps hazards off the player after.
        /// </summary>
        public bool TryRespawn(float delay, float graceTime, Action<GameObject> afterRespawn = null)
        {
            if (IsRespawning || InRespawnGrace) return false;
            StartCoroutine(RespawnRoutine(delay, graceTime, afterRespawn));
            return true;
        }

        private IEnumerator RespawnRoutine(float delay, float graceTime, Action<GameObject> afterRespawn)
        {
            IsRespawning = true;
            GameObject player = ResolvePlayer();

            // Lock control and drop the fall pose immediately, then fade with no pre-delay so the
            // player never hangs visibly in mid-air waiting to respawn.
            SetPlayerControl(player, false);

            ScreenFadeController fade = ScreenFadeController.Instance;
            if (fade != null) yield return fade.FadeOut();

            // Any hazard "delay before respawn" now passes under black — never a visible freeze.
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

            if (player != null)
            {
                GetSpawnPose(out Vector3 pos, out Quaternion rot);
                TeleportPlayer(player, pos, rot);   // teleport + state reset + camera warp
                afterRespawn?.Invoke(player);        // hazard damage
                PlayRespawnVfx(pos, rot);            // respawn VFX (checkpoint may override)
                PlayerRespawned?.Invoke(player);
            }

            if (fade != null)
            {
                yield return fade.HoldBlack();
                yield return fade.FadeIn();
            }

            // Control only returns once the screen is fully back.
            SetPlayerControl(player, true);
            _graceUntil = Time.time + Mathf.Max(0f, graceTime);
            IsRespawning = false;
        }

        private void PlayRespawnVfx(Vector3 pos, Quaternion rot)
        {
            if (!playRespawnVfx) return;
            GameObject prefab = ActiveCheckpoint != null && ActiveCheckpoint.RespawnVfxOverride != null
                ? ActiveCheckpoint.RespawnVfxOverride
                : respawnVfxPrefab;
            if (prefab != null) VfxPool.Play(prefab, pos, rot);
        }

        private static void SetPlayerControl(GameObject player, bool enabled)
        {
            if (player == null) return;

            var input = player.GetComponent<PlayerInput>();
            if (input != null)
            {
                if (enabled) input.ActivateInput();
                else input.DeactivateInput();
            }

            // While control is locked, stop the movement animator too — otherwise it keeps driving
            // the fall/land layer off the frozen airborne data. Snap it grounded first so the fall
            // pose doesn't hang during the fade.
            var movementAnimator = player.GetComponent<PlayerMovementAnimator>();
            if (!enabled) movementAnimator?.SnapToGrounded();

            SetEnabled(player.GetComponent<PlayerController>(), enabled);
            SetEnabled(player.GetComponent<PlayerDodge>(), enabled);
            SetEnabled(player.GetComponent<PlayerCombat>(), enabled);
            SetEnabled(movementAnimator, enabled);
        }

        private static void SetEnabled(MonoBehaviour behaviour, bool enabled)
        {
            if (behaviour != null) behaviour.enabled = enabled;
        }

        private void TeleportPlayer(GameObject player, Vector3 pos, Quaternion rot)
        {
            // A respawn is a hard reset land in normal time even if a hit-stop / slow-mo was mid-flight.
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;

            Quaternion finalRot = preserveFacing ? player.transform.rotation : rot;
            Vector3 delta = pos - player.transform.position;

            // CharacterController overrides transform writes unless disabled around the teleport.
            var controller = player.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            player.transform.SetPositionAndRotation(pos, finalRot);
            if (controller != null) controller.enabled = true;

            var rb = player.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            // Refresh grounded state + the animator at the new position so locomotion resumes at
            // once — no lingering fall/land driven by the old airborne velocity/grounded data.
            player.GetComponent<PlayerController>()?.SnapToGround();
            player.GetComponent<PlayerMovementAnimator>()?.SnapToGrounded();
            player.GetComponent<PlayerDodge>()?.CancelDodge();
            player.GetComponent<PlayerCombat>()?.CancelAttack();

            if (cameraReset) WarpCameras(player.transform, delta);
        }

        private void GetSpawnPose(out Vector3 pos, out Quaternion rot)
        {
            if (ActiveCheckpoint != null)
            {
                pos = ActiveCheckpoint.SpawnPosition;
                rot = ActiveCheckpoint.SpawnRotation;
                return;
            }
            CaptureDefaultSpawn();
            pos = _defaultPosition;
            rot = _defaultRotation;
        }

        private void CaptureDefaultSpawn()
        {
            if (_defaultCaptured) return;

            if (defaultSpawnPoint != null)
            {
                _defaultPosition = defaultSpawnPoint.position;
                _defaultRotation = defaultSpawnPoint.rotation;
                _defaultCaptured = true;
                return;
            }

            GameObject player = ResolvePlayer();
            if (player != null)
            {
                _defaultPosition = player.transform.position;
                _defaultRotation = player.transform.rotation;
                _defaultCaptured = true;
            }
        }

        private static void WarpCameras(Transform target, Vector3 delta)
        {
            foreach (CinemachineCamera cam in FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None))
                cam.OnTargetObjectWarped(target, delta);
        }

        private GameObject ResolvePlayer()
        {
            if (_player == null) _player = GameObject.FindWithTag("Player");
            return _player;
        }
    }
}
