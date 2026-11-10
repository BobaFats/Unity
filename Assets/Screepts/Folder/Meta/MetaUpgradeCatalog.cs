using System;
using System.Collections.Generic;
using UnityEngine;

namespace Tanks2D
{
    // Что даёт улучшение наследия в начале каждого нового забега
    public enum MetaEffect
    {
        WallHPPercent,   // + % прочности стены
        DamagePercent,   // + % урона пули
        StartHorseshoes, // + подковы на старте
        StartTowers      // + башни на старте
    }

    [Serializable]
    public class MetaUpgrade
    {
        [Tooltip("Уникальный ключ. По нему хранится купленный уровень — не менять после выпуска")]
        public string id = "upgrade";
        public string title = "Улучшение";
        [Tooltip("{0} — подставится величина на текущем уровне")]
        public string description = "+{0}";
        public MetaEffect effect;
        [Tooltip("Прибавка за каждый уровень")]
        public int amountPerLevel = 10;
        [Min(1)] public int maxLevel = 5;
        [Tooltip("Цена первого уровня в славе")]
        [Min(1)] public int baseCost = 50;
        [Tooltip("Каждый следующий уровень дороже во столько раз")]
        [Min(1f)] public float costGrowth = 1.6f;

        public int CostOf(int level) => Mathf.RoundToInt(baseCost * Mathf.Pow(costGrowth, level));
        public string Describe(int level) => string.Format(description, amountPerLevel * Mathf.Max(1, level));
    }

    // Улучшения наследия (Assets/Resources/MetaUpgradeCatalog.asset): покупаются за славу между забегами,
    // действуют на каждый новый забег. Цены и силу правят в ассете.
    [CreateAssetMenu(fileName = "MetaUpgradeCatalog", menuName = "Shooter/Meta Upgrade Catalog")]
    public class MetaUpgradeCatalog : ScriptableObject
    {
        public const string ResourcePath = "MetaUpgradeCatalog";

        [SerializeField] private List<MetaUpgrade> _upgrades = new List<MetaUpgrade>();

        private static MetaUpgradeCatalog _instance;

        public static MetaUpgradeCatalog Instance
        {
            get
            {
                if (_instance == null) _instance = Resources.Load<MetaUpgradeCatalog>(ResourcePath);
                return _instance;
            }
        }

        public static void SetInstance(MetaUpgradeCatalog catalog) => _instance = catalog;

        public IReadOnlyList<MetaUpgrade> Upgrades => _upgrades;

        public MetaUpgrade Find(string id) => _upgrades.Find(u => u != null && u.id == id);

        public bool AddIfMissing(MetaUpgrade upgrade)
        {
            if (upgrade == null || Find(upgrade.id) != null) return false;
            _upgrades.Add(upgrade);
            return true;
        }

        // Новый забег: купленные улучшения применяются к стартовым характеристикам
        public void ApplyToNewRun()
        {
            foreach (MetaUpgrade upgrade in _upgrades)
            {
                if (upgrade == null) continue;
                int level = MetaProgress.LevelOf(upgrade.id);
                if (level <= 0) continue;

                int amount = upgrade.amountPerLevel * level;
                switch (upgrade.effect)
                {
                    case MetaEffect.WallHPPercent:
                        GameStats.WallMaxHP = Mathf.Min(GameStats.MaxWallHP, Mathf.RoundToInt(GameStats.WallMaxHP * (1f + amount / 100f)));
                        break;
                    case MetaEffect.DamagePercent:
                        GameStats.BulletDamage = Mathf.Max(GameStats.BulletDamage + 1, Mathf.RoundToInt(GameStats.BulletDamage * (1f + amount / 100f)));
                        break;
                    case MetaEffect.StartHorseshoes:
                        GameStats.Horseshoes += amount;
                        break;
                    case MetaEffect.StartTowers:
                        GameStats.TowerCount = Mathf.Min(GameStats.MaxTowers, GameStats.TowerCount + amount);
                        break;
                }
            }
        }
    }
}
