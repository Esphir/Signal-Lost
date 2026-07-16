using Signal.VFX;
using UnityEngine;

namespace Signal.Tutorial
{
    /// <summary>
    /// Basic combat tutorial: spawns a training dummy and completes only once the player has
    /// performed at least one Light attack AND one Heavy attack (watched via PlayerCombat's
    /// AttackStarted event — no polling of combat internals).
    /// </summary>
    public class BasicCombatStep : TutorialStep
    {
        [SerializeField] private TutorialEnemySpawner spawner;
        [SerializeField] private bool requireLight = true;
        [SerializeField] private bool requireHeavy = true;

        private PlayerCombat _combat;
        private bool _didLight;
        private bool _didHeavy;

        protected override void OnBegin()
        {
            _didLight = false;
            _didHeavy = false;
            if (spawner != null) spawner.SpawnAll();

            GameObject player = GameObject.FindWithTag("Player");
            _combat = player != null ? player.GetComponent<PlayerCombat>() : null;
            if (_combat != null) _combat.AttackStarted += OnAttack;
            else Complete();
        }

        private void OnAttack(PlayerAttackKind kind)
        {
            if (kind == PlayerAttackKind.Light) _didLight = true;
            else if (kind == PlayerAttackKind.Heavy) _didHeavy = true;

            if ((!requireLight || _didLight) && (!requireHeavy || _didHeavy))
                Complete();
        }

        protected override void OnEnd()
        {
            if (_combat != null) _combat.AttackStarted -= OnAttack;
            if (spawner != null) spawner.Clear();
        }
    }
}
