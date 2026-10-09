using System.Collections.Generic;
using UnityEngine;

namespace Tanks2D
{
    // Все прокачиваемые характеристики в одном месте.
    // Живут между сценами (лобби -> бой -> лобби) и сбрасываются только при новой игре.
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
        public const int XpWallHPStep = 50;
        public const int MaxWallHP = 1000;

        // Кампания
        public const int TotalLevels = 30;

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

        // Кампания: номер текущего уровня 1..30 (= уровень босса) и сколько боссов побеждено
        public static int Mission { get; set; }
        public static int BossesDefeated { get; set; }
        public static bool CampaignCompleted { get; set; }

        // Выбранный скин оружия героя (id из WeaponSkinCatalog; пусто — первый в каталоге)
        public static string WeaponSkinId { get; set; }

        // Уровни стихий пуль по id стихии (0 — не изучена)
        private static readonly Dictionary<string, int> _elementLevels = new Dictionary<string, int>();

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
            Mission = 1;
            BossesDefeated = 0;
            CampaignCompleted = false;
            _elementLevels.Clear();
        }

        public static int GetElementLevel(string elementId)
        {
            return elementId != null && _elementLevels.TryGetValue(elementId, out int level) ? level : 0;
        }

        public static void SetElementLevel(string elementId, int level)
        {
            if (string.IsNullOrEmpty(elementId)) return;
            _elementLevels[elementId] = Mathf.Max(0, level);
        }
    }
}
