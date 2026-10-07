using System.Collections.Generic;
using UnityEngine;

namespace Tanks2D
{
    public enum UpgradeType
    {
        Damage,
        FireRate,
        ReloadTime,
        MaxAmmo,
        WallHP
    }

    // Все прокачиваемые характеристики в одном месте.
    // Живут между сценами (бой -> лагерь -> бой) и сбрасываются только при новой игре / рестарте.
    public static class GameStats
    {
        public const int BaseBulletDamage = 10;
        public const float BaseFireDelay = 0.4f;
        public const float BaseReloadTime = 2.0f;
        public const int BaseMaxAmmo = 15;
        public const int BaseWallMaxHP = 100;

        // Оружие и помощники (прокачка за опыт)
        public const int BaseBulletCount = 1;
        public const int MaxBulletCount = 7;
        public const float BaseSpreadAngle = 12f;
        public const float SpreadStep = 8f;
        public const float MaxSpreadAngle = 70f;
        public const int XpDamageStep = 3;
        public const int MaxTowers = 5;

        public static int BulletDamage { get; set; }
        public static float FireDelay { get; set; }
        public static float ReloadTime { get; set; }
        public static int MaxAmmo { get; set; }
        public static int WallMaxHP { get; set; }

        public static int BulletCount { get; set; }
        // Полный угол веера пуль в градусах (имеет смысл при BulletCount > 1)
        public static float SpreadAngle { get; set; }
        public static int TowerCount { get; set; }

        // Опыт
        public static int Level { get; set; }
        public static int Experience { get; set; }

        private static readonly Dictionary<UpgradeType, int> _levels = new Dictionary<UpgradeType, int>();

        static GameStats()
        {
            ResetAll();
        }

        // Сброс и при входе в Play Mode с выключенным Domain Reload
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad() => ResetAll();

        public static void ResetAll()
        {
            BulletDamage = BaseBulletDamage;
            FireDelay = BaseFireDelay;
            ReloadTime = BaseReloadTime;
            MaxAmmo = BaseMaxAmmo;
            WallMaxHP = BaseWallMaxHP;
            BulletCount = BaseBulletCount;
            SpreadAngle = BaseSpreadAngle;
            TowerCount = 0;
            Level = 1;
            Experience = 0;
            _levels.Clear();
        }

        public static int GetLevel(UpgradeType type)
        {
            return _levels.TryGetValue(type, out int level) ? level : 1;
        }

        public static void IncrementLevel(UpgradeType type)
        {
            _levels[type] = GetLevel(type) + 1;
        }

        public static float GetValue(UpgradeType type)
        {
            switch (type)
            {
                case UpgradeType.Damage: return BulletDamage;
                case UpgradeType.FireRate: return FireDelay;
                case UpgradeType.ReloadTime: return ReloadTime;
                case UpgradeType.MaxAmmo: return MaxAmmo;
                case UpgradeType.WallHP: return WallMaxHP;
                default: return 0f;
            }
        }

        public static void SetValue(UpgradeType type, float value)
        {
            switch (type)
            {
                case UpgradeType.Damage: BulletDamage = Mathf.RoundToInt(value); break;
                case UpgradeType.FireRate: FireDelay = value; break;
                case UpgradeType.ReloadTime: ReloadTime = value; break;
                case UpgradeType.MaxAmmo: MaxAmmo = Mathf.RoundToInt(value); break;
                case UpgradeType.WallHP: WallMaxHP = Mathf.RoundToInt(value); break;
            }
        }
    }
}
