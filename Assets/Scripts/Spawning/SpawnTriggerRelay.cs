using UnityEngine;

namespace Signal.Spawning
{
    /// <summary>
    /// Forwards trigger events to an <see cref="EnemySpawnSection"/> when that section's trigger
    /// volume sits on a different GameObject — Unity only delivers OnTriggerEnter to the collider's
    /// own object. Added automatically by the section; not something to place by hand.
    /// </summary>
    [DisallowMultipleComponent]
    public class SpawnTriggerRelay : MonoBehaviour
    {
        private EnemySpawnSection _section;

        internal static void Attach(GameObject host, EnemySpawnSection section)
        {
            var relay = host.GetComponent<SpawnTriggerRelay>();
            if (relay == null) relay = host.AddComponent<SpawnTriggerRelay>();
            relay._section = section;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_section != null) _section.HandleTriggerEnter(other);
        }
    }
}
