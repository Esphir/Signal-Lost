using UnityEngine;

namespace Signal.Generation
{
    /// <summary>Slowly spins a dropped key so it reads as an objective/reward marker at the exit.</summary>
    [DisallowMultipleComponent]
    public sealed class KeySpinner : MonoBehaviour
    {
        [SerializeField] private float degreesPerSecond = 60f;

        private void Update() => transform.Rotate(Vector3.up, degreesPerSecond * Time.deltaTime, Space.World);
    }
}
