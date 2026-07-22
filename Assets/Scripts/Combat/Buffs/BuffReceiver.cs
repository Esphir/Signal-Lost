// Manages timed buffs on this GameObject: applies effects, refreshes durations on re-application, expires them, and raises events for visuals/UI.
using System;
using System.Collections.Generic;
using UnityEngine;
using Signal.Combat.Interfaces;

namespace Signal.Combat.Buffs
{
    public class BuffReceiver : MonoBehaviour, IBuffable
    {
        private struct ActiveBuff
        {
            public BuffSO Definition;
            public IBuffEffect Effect;
            public float ExpiresAt;
        }

        private readonly List<ActiveBuff> _active = new List<ActiveBuff>();

        public event Action<BuffSO> BuffApplied;
        public event Action<BuffSO> BuffExpired;

        public int ActiveBuffCount => _active.Count;

        public bool ApplyBuff(BuffSO buff)
        {
            if (buff == null) return false;

            for (int i = 0; i < _active.Count; i++)
            {
                if (_active[i].Definition != buff) continue;

                ActiveBuff refreshed = _active[i];
                refreshed.ExpiresAt = Time.time + buff.duration;
                _active[i] = refreshed;
                CombatLog.Info($"'{name}' buff '{buff.name}' refreshed ({buff.duration:0.#}s).", this);
                return true;
            }

            IBuffEffect effect = buff.CreateEffect();
            effect.Apply(gameObject);
            _active.Add(new ActiveBuff
            {
                Definition = buff,
                Effect = effect,
                ExpiresAt = Time.time + buff.duration
            });

            CombatLog.Info($"'{name}' gained buff '{buff.name}' for {buff.duration:0.#}s.", this);
            BuffApplied?.Invoke(buff);
            return true;
        }

        private void Update()
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                if (Time.time < _active[i].ExpiresAt) continue;

                ActiveBuff expired = _active[i];
                _active.RemoveAt(i);
                expired.Effect.Remove(gameObject);
                CombatLog.Info($"'{name}' buff '{expired.Definition.name}' expired.", this);
                BuffExpired?.Invoke(expired.Definition);
            }
        }

        private void OnDestroy()
        {
            for (int i = _active.Count - 1; i >= 0; i--)
                _active[i].Effect.Remove(gameObject);
            _active.Clear();
        }
    }
}
