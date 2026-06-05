using System.Collections;
using UnityEngine;

/// <summary>
/// Simple procedural camera shake.
/// Attach to the Camera (or its parent rig) and call Shake() from PlayerCombat.
/// Uses Perlin noise for smooth, organic movement.
/// </summary>
public class CameraShake : MonoBehaviour
{
    [Tooltip("Maximum shake offset applied per axis.")]
    public float maxOffset = 0.5f;

    [Tooltip("Speed of noise scrolling — higher = more jittery.")]
    public float noiseSpeed = 25f;

    // ── Private ───────────────────────────────────────────────────────────
    private Vector3 _originLocalPos;
    private float   _shakeAmount;
    private float   _shakeTimer;
    private float   _shakeDuration;
    private float   _noiseOffset;

    // ──────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _originLocalPos = transform.localPosition;
        _noiseOffset    = Random.Range(0f, 100f);
    }

    private void LateUpdate()
    {
        if (_shakeTimer <= 0f)
        {
            transform.localPosition = _originLocalPos;
            return;
        }

        _shakeTimer -= Time.unscaledDeltaTime; // unscaled so it works during hit-stop

        float t        = _shakeTimer / _shakeDuration;
        float strength = _shakeAmount * t * maxOffset; // fade out over duration

        float nx = (Mathf.PerlinNoise(_noiseOffset + Time.unscaledTime * noiseSpeed, 0f) - 0.5f) * 2f;
        float ny = (Mathf.PerlinNoise(0f, _noiseOffset + Time.unscaledTime * noiseSpeed) - 0.5f) * 2f;

        transform.localPosition = _originLocalPos + new Vector3(nx, ny, 0f) * strength;
    }

    // ── Public API ────────────────────────────────────────────────────────

    /// <summary>Trigger a shake. Safe to call mid-shake — picks the stronger value.</summary>
    public void Shake(float amount, float duration)
    {
        _shakeAmount   = Mathf.Max(_shakeAmount, amount);
        _shakeDuration = duration;
        _shakeTimer    = duration;
    }
}
