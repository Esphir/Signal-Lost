// A trigger volume that raises PlayerEntered when the player walks in.
using System;
using UnityEngine;

namespace Signal.Tutorial
{
    public class TutorialTrigger : MonoBehaviour
    {
        [SerializeField] private Vector3 size = new Vector3(3f, 3f, 3f);
        [SerializeField] private Vector3 center = new Vector3(0f, 1.5f, 0f);

        public event Action PlayerEntered;

        private void Awake()
        {
            foreach (Collider c in GetComponents<Collider>())
                if (c.isTrigger) return;

            var box = gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = center;
            box.size = size;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player")) PlayerEntered?.Invoke();
        }
    }
}
