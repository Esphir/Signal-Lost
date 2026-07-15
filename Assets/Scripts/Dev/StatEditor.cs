using System;
using System.Collections.Generic;
using Signal.Combat.Data;
using Signal.Combat.Health;
using Signal.Run;
using Signal.Stats;
using Signal.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Signal.Dev
{
    /// <summary>
    /// Developer "Player" tab: shows the player's final calculated stats (read through the same
    /// <see cref="RunManager.QueryStat"/> the game uses) and edits them by feeding run upgrades back
    /// into <see cref="RunManager.AddUpgrade"/> — never by poking private variables. Also exposes the
    /// player-action buttons (kill, heal, give…, reset).
    /// </summary>
    public class StatEditor : MonoBehaviour
    {
        [SerializeField, Min(0.1f)]
        [Tooltip("Reference base used only to display final Attack Damage (damage base varies per attack).")]
        private float referenceAttackDamage = 10f;

        private static readonly Color RowColor = new Color(1f, 1f, 1f, 0.04f);
        private static readonly Color ButtonColor = new Color(0.16f, 0.16f, 0.22f);

        private HealthComponent _health;
        private PlayerController _controller;
        private float _step = 5f;

        public void BuildPlayerTab(Transform parent, List<Action> refreshers)
        {
            ResolvePlayer();

            BuildStepControl(parent);

            AddRow(parent, refreshers, "Current HP",
                () => _health != null ? $"{_health.CurrentHealth:0} / {_health.MaxHealth:0}" : "—",
                () => Damage(_step), () => Heal(_step));
            AddRow(parent, refreshers, "Max HP",
                () => _health != null ? $"{_health.MaxHealth:0}" : "—",
                () => Give(StatType.MaxHealth, StatModifierMode.Flat, -_step), () => Give(StatType.MaxHealth, StatModifierMode.Flat, _step));
            AddRow(parent, refreshers, "Attack Damage",
                () => $"{RunManager.QueryStat(StatType.AttackDamage, referenceAttackDamage):0.#}",
                () => Give(StatType.AttackDamage, StatModifierMode.Flat, -_step), () => Give(StatType.AttackDamage, StatModifierMode.Flat, _step));
            AddRow(parent, refreshers, "Crit Chance",
                () => $"{RunManager.QueryStat(StatType.CritChance, 0f):0.#}%",
                () => Give(StatType.CritChance, StatModifierMode.Flat, -_step), () => Give(StatType.CritChance, StatModifierMode.Flat, _step));
            AddRow(parent, refreshers, "Attack Speed",
                () => $"{RunManager.QueryStat(StatType.AttackSpeed, 1f):0.00}x",
                () => Give(StatType.AttackSpeed, StatModifierMode.Percent, -_step), () => Give(StatType.AttackSpeed, StatModifierMode.Percent, _step));
            AddRow(parent, refreshers, "Movement Speed",
                () => $"{RunManager.QueryStat(StatType.MoveSpeed, BaseMoveSpeed):0.#} m/s",
                () => Give(StatType.MoveSpeed, StatModifierMode.Percent, -_step), () => Give(StatType.MoveSpeed, StatModifierMode.Percent, _step));
            AddRow(parent, refreshers, "Life Steal",
                () => $"{RunManager.QueryStat(StatType.Lifesteal, 0f):0.#}%",
                () => Give(StatType.Lifesteal, StatModifierMode.Flat, -_step), () => Give(StatType.Lifesteal, StatModifierMode.Flat, _step));

            UiBuilder.CreateText(parent, "ActionsHeader", "Player Actions", 20, FontStyle.Bold, TextAnchor.MiddleLeft)
                .gameObject.AddComponent<LayoutElement>().preferredHeight = 34f;

            AddAction(parent, "Kill Player", KillPlayer);
            AddAction(parent, "Heal to Full", HealToFull);
            AddAction(parent, "Give Max Health", () => Give(StatType.MaxHealth, StatModifierMode.Flat, 20f));
            AddAction(parent, "Give Attack Damage", () => Give(StatType.AttackDamage, StatModifierMode.Flat, 5f));
            AddAction(parent, "Give Crit Chance", () => Give(StatType.CritChance, StatModifierMode.Flat, 5f));
            AddAction(parent, "Give Attack Speed", () => Give(StatType.AttackSpeed, StatModifierMode.Percent, 10f));
            AddAction(parent, "Give Move Speed", () => Give(StatType.MoveSpeed, StatModifierMode.Percent, 10f));
            AddAction(parent, "Give Life Steal", () => Give(StatType.Lifesteal, StatModifierMode.Flat, 3f));
            AddAction(parent, "Reset Run Stats", ResetRunStats);
        }

        private float BaseMoveSpeed => _controller != null ? _controller.moveSpeed : 7f;

        // ── Actions (all through existing public APIs) ────────────────────────

        private void KillPlayer()
        {
            ResolvePlayer();
            if (_health == null) return;
            // Close the dev menu first so the Run End screen isn't hidden behind it, then route
            // through the normal damage path so Died → EndRun → Run End screen all fire.
            GetComponent<DeveloperMenu>()?.CloseMenu();
            _health.TakeDamage(new DamageInfo(_health.MaxHealth + 999f, gameObject));
        }

        private void HealToFull()
        {
            ResolvePlayer();
            if (_health != null) _health.Heal(_health.MaxHealth);
        }

        private void Heal(float amount)
        {
            ResolvePlayer();
            if (_health != null) _health.Heal(Mathf.Abs(amount));
        }

        private void Damage(float amount)
        {
            ResolvePlayer();
            if (_health != null) _health.TakeDamage(new DamageInfo(Mathf.Abs(amount), gameObject));
        }

        private void Give(StatType stat, StatModifierMode mode, float value)
        {
            if (!RunManager.HasInstance || Mathf.Approximately(value, 0f)) return;
            RunManager.Instance.AddUpgrade(new RunUpgrade
            {
                modifier = new StatModifier { stat = stat, mode = mode, value = value },
                rarity = ItemRarity.Common,
                label = $"Dev {(value >= 0 ? "+" : "")}{value:0.#} {stat}",
            });
        }

        private void ResetRunStats()
        {
            if (RunManager.HasInstance) RunManager.Instance.StartRun();
        }

        // ── UI construction ───────────────────────────────────────────────────

        private void BuildStepControl(Transform parent)
        {
            Image row = UiBuilder.NewChild<Image>(parent, "StepRow");
            row.color = RowColor;
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 44f;

            Text label = UiBuilder.CreateText(row.transform, "Label", "Step Amount", 18, FontStyle.Normal, TextAnchor.MiddleLeft);
            label.rectTransform.anchorMin = new Vector2(0f, 0f);
            label.rectTransform.anchorMax = new Vector2(0.28f, 1f);
            label.rectTransform.offsetMin = new Vector2(10f, 0f);
            label.rectTransform.offsetMax = Vector2.zero;

            Slider slider = UiBuilder.CreateSlider(row.transform, "Slider", 1f, 50f, _step);
            var sliderRect = (RectTransform)slider.transform;
            sliderRect.anchorMin = new Vector2(0.3f, 0.25f);
            sliderRect.anchorMax = new Vector2(0.75f, 0.75f);
            sliderRect.offsetMin = Vector2.zero;
            sliderRect.offsetMax = Vector2.zero;

            InputField field = UiBuilder.CreateInputField(row.transform, "Field", _step.ToString("0.#"));
            var fieldRect = (RectTransform)field.transform;
            fieldRect.anchorMin = new Vector2(0.78f, 0.2f);
            fieldRect.anchorMax = new Vector2(0.98f, 0.8f);
            fieldRect.offsetMin = Vector2.zero;
            fieldRect.offsetMax = Vector2.zero;

            slider.onValueChanged.AddListener(v =>
            {
                _step = v;
                field.SetTextWithoutNotify(v.ToString("0.#"));
            });
            field.onEndEdit.AddListener(t =>
            {
                if (float.TryParse(t, out float v))
                {
                    _step = Mathf.Clamp(v, 1f, 50f);
                    slider.SetValueWithoutNotify(_step);
                }
            });
        }

        private void AddRow(Transform parent, List<Action> refreshers, string name, Func<string> read, Action onMinus, Action onPlus)
        {
            Image row = UiBuilder.NewChild<Image>(parent, $"Row_{name}");
            row.color = RowColor;
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 40f;

            var hl = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.padding = new RectOffset(10, 10, 4, 4);
            hl.spacing = 8f;
            hl.childAlignment = TextAnchor.MiddleLeft;
            hl.childControlWidth = true;
            hl.childForceExpandWidth = false;
            hl.childControlHeight = true;
            hl.childForceExpandHeight = true;

            UiBuilder.CreateText(row.transform, "Label", name, 18, FontStyle.Normal, TextAnchor.MiddleLeft)
                .gameObject.AddComponent<LayoutElement>().preferredWidth = 180f;

            Text value = UiBuilder.CreateText(row.transform, "Value", read(), 18, FontStyle.Bold, TextAnchor.MiddleCenter);
            value.gameObject.AddComponent<LayoutElement>().preferredWidth = 130f;

            AddSmallButton(row.transform, "-", onMinus);
            AddSmallButton(row.transform, "+", onPlus);

            refreshers.Add(() => value.text = read());
        }

        private void AddSmallButton(Transform parent, string label, Action onClick)
        {
            Button button = UiBuilder.CreateButton(parent, $"Btn_{label}", label, ButtonColor, 20, out _);
            button.gameObject.AddComponent<LayoutElement>().preferredWidth = 46f;
            button.onClick.AddListener(() => onClick?.Invoke());
        }

        private void AddAction(Transform parent, string label, Action onClick)
        {
            Button button = UiBuilder.CreateButton(parent, $"Action_{label}", label, ButtonColor, 18, out _);
            button.gameObject.AddComponent<LayoutElement>().preferredHeight = 38f;
            button.onClick.AddListener(() => onClick?.Invoke());
        }

        private void ResolvePlayer()
        {
            if (_health != null && _controller != null) return;
            GameObject player = GameObject.FindWithTag("Player");
            if (player == null) return;
            _health = player.GetComponent<HealthComponent>();
            _controller = player.GetComponent<PlayerController>();
        }
    }
}
