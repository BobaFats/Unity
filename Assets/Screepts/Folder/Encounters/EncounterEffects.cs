using System.Collections.Generic;
using UnityEngine;

namespace Tanks2D
{
    // Применение исходов встреч к прогрессу героя
    public static class EncounterEffects
    {
        // Колёса дилижанса меняет текущая поездка (если она идёт)
        public static System.Action<int> WheelsChanged;

        public static void Apply(IEnumerable<EncounterEffect> effects)
        {
            if (effects == null) return;
            foreach (EncounterEffect effect in effects) Apply(effect);
        }

        public static void Apply(EncounterEffect effect)
        {
            if (effect == null) return;
            int amount = effect.amount;

            switch (effect.type)
            {
                case EncounterEffectType.Experience:
                    GameStats.Experience = Mathf.Max(0, GameStats.Experience + amount);
                    break;

                case EncounterEffectType.BulletDamagePercent:
                    GameStats.BulletDamage = Mathf.Max(1, Mathf.RoundToInt(GameStats.BulletDamage * (1f + amount / 100f)));
                    break;

                case EncounterEffectType.AddTower:
                    GameStats.TowerCount = Mathf.Clamp(GameStats.TowerCount + amount, 0, GameStats.MaxTowers);
                    break;

                case EncounterEffectType.Wheels:
                    WheelsChanged?.Invoke(amount);
                    break;

                case EncounterEffectType.WallMaxHP:
                    GameStats.WallMaxHP = Mathf.Clamp(GameStats.WallMaxHP + amount, 50, GameStats.MaxWallHP);
                    break;

                case EncounterEffectType.WallDamageNextBattle:
                    GameStats.NextBattleWallDamage = Mathf.Clamp01(GameStats.NextBattleWallDamage + amount / 100f);
                    break;

                case EncounterEffectType.ElementLevel:
                    RaiseRandomElement(amount);
                    break;

                case EncounterEffectType.Horseshoes:
                    GameStats.Horseshoes = Mathf.Max(0, GameStats.Horseshoes + amount);
                    break;
            }
        }

        private static void RaiseRandomElement(int levels)
        {
            ElementCatalog catalog = ElementCatalog.Instance;
            if (catalog == null) return;

            var candidates = new List<ElementDefinition>();
            foreach (ElementDefinition element in catalog.Elements) if (element != null && !element.IsMaxed) candidates.Add(element);
            if (candidates.Count == 0) return;

            ElementDefinition chosen = candidates[Random.Range(0, candidates.Count)];
            chosen.Level = chosen.Level + Mathf.Max(1, levels);
        }

        // Короткое описание для карты исхода: «+15 опыта, −1 колесо»
        public static string Describe(IEnumerable<EncounterEffect> effects)
        {
            if (effects == null) return "";
            var parts = new List<string>();
            foreach (EncounterEffect effect in effects) parts.Add(Describe(effect));
            return string.Join(", ", parts);
        }

        public static string Describe(EncounterEffect effect)
        {
            string sign = effect.amount >= 0 ? "+" : "−";
            int value = Mathf.Abs(effect.amount);

            switch (effect.type)
            {
                case EncounterEffectType.Experience: return $"{sign}{value} опыта";
                case EncounterEffectType.BulletDamagePercent: return $"урон {sign}{value}%";
                case EncounterEffectType.AddTower: return $"{sign}{value} башня";
                case EncounterEffectType.Wheels: return $"{sign}{value} колесо";
                case EncounterEffectType.WallMaxHP: return $"стена {sign}{value} HP";
                case EncounterEffectType.WallDamageNextBattle: return $"стена повреждена на {value}% к следующему бою";
                case EncounterEffectType.ElementLevel: return $"+{value} к случайной стихии";
                case EncounterEffectType.Horseshoes: return $"{sign}{value} подков";
                default: return effect.type.ToString();
            }
        }
    }
}
