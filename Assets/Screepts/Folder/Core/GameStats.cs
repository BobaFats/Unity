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
        public const int BaseWallMaxHP = 300;

        // Оружие и помощники (прокачка за опыт)
        public const int BaseBulletCount = 1;
        public const int MaxBulletCount = 7;
        public const float BaseSpreadAngle = 12f;
        public const float SpreadStep = 8f;
        public const float MaxSpreadAngle = 70f;
        public const int XpDamageStep = 2;            // минимальная прибавка урона за выбор
        public const float XpDamageMultiplier = 1.2f; // урон ×1.2 за выбор — растёт вместе со здоровьем врагов

        public static int NextDamage(int damage) => Mathf.Max(damage + XpDamageStep, Mathf.RoundToInt(damage * XpDamageMultiplier));
        public const int MaxTowers = 5;
        public const int XpWallHPStep = 50;
        public const int MaxWallHP = 1000;

        // Навыки за уровень (шаги и пределы)
        public const float FireDelayStep = 0.9f;      // ×0.9 к задержке выстрела
        public const float MinFireDelay = 0.12f;
        public const float ReloadTimeStep = 0.85f;    // ×0.85 к времени перезарядки
        public const float MinReloadTime = 0.4f;
        public const int AmmoStep = 5;
        public const int MaxAmmoLimit = 60;
        public const int MaxPierce = 5;
        public const int MaxExplosionLevel = 5;
        public const int MaxTowerPowerLevel = 5;
        public const float WallRepairFraction = 0.5f;

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

        // Сколько врагов пуля пробивает насквозь (0 — исчезает на первом)
        public static int Pierce { get; set; }
        // Взрывные пули: 0 — нет, дальше радиус и доля урона растут
        public static int ExplosionLevel { get; set; }
        // Сила башен: урон и скорострельность
        public static int TowerPowerLevel { get; set; }

        public static float ExplosionRadius => ExplosionLevel <= 0 ? 0f : 0.6f + 0.25f * ExplosionLevel;
        public static float ExplosionDamageFraction => ExplosionLevel <= 0 ? 0f : 0.3f + 0.1f * ExplosionLevel;
        public static float TowerDamageMultiplier => 1f + 0.25f * TowerPowerLevel;
        public static float TowerFireDelayMultiplier => Mathf.Pow(0.88f, TowerPowerLevel);

        // Опыт
        public static int Level { get; set; }
        public static int Experience { get; set; }

        // Кампания: номер текущего уровня 1..30 (= уровень босса) и сколько боссов побеждено
        public static int Mission { get; set; }
        public static int BossesDefeated { get; set; }
        public static bool CampaignCompleted { get; set; }

        // Выбранный скин оружия героя (id из WeaponSkinCatalog; пусто — первый в каталоге)
        public static string WeaponSkinId { get; set; }

        // Город, в котором сейчас герой (id из TownCatalog; пусто — город текущего уровня)
        public static string CurrentTownId { get; set; }

        // Забег: карта городов (см. CampaignRun), подковы из поездок, штраф стене после аварии дилижанса
        public static RunMap Map { get; set; }
        public static int Horseshoes { get; set; }
        // Доля прочности стены, потерянная к началу следующего боя (0.3 = -30%)
        public static float NextBattleWallDamage { get; set; }

        // Забег: свой номер (сохранение относится только к своему забегу), идёт ли сейчас бой и статистика для итогов
        public static int RunId { get; set; }
        public static bool InBattle { get; set; }
        public static int RunKills { get; set; }
        public static int RunDefeats { get; set; }
        public static float RunPlayTime { get; set; }

        // Уровни стихий пуль по id стихии (0 — не изучена)
        private static readonly Dictionary<string, int> _elementLevels = new Dictionary<string, int>();

        // Сюжет забега: флаги мира (последствия заданий, по ним задания связаны между собой) и выполненные задания
        private static readonly HashSet<string> _storyFlags = new HashSet<string>();
        private static readonly HashSet<string> _completedQuests = new HashSet<string>();

        public static IReadOnlyCollection<string> StoryFlags => _storyFlags;
        public static IReadOnlyCollection<string> CompletedQuests => _completedQuests;
        public static IReadOnlyDictionary<string, int> ElementLevels => _elementLevels;

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
            Pierce = 0;
            ExplosionLevel = 0;
            TowerPowerLevel = 0;
            Level = 1;
            Experience = 0;
            Mission = 1;
            BossesDefeated = 0;
            CampaignCompleted = false;
            CurrentTownId = null;
            Map = null;
            Horseshoes = 0;
            NextBattleWallDamage = 0f;
            RunId = System.Guid.NewGuid().GetHashCode(); // не UnityEngine.Random: ResetAll вызывается и из статического конструктора
            InBattle = false;
            RunKills = 0;
            RunDefeats = 0;
            RunPlayTime = 0f;
            _elementLevels.Clear();
            _storyFlags.Clear();
            _completedQuests.Clear();
        }

        public static bool HasStoryFlag(string flag)
        {
            return !string.IsNullOrWhiteSpace(flag) && _storyFlags.Contains(flag.Trim());
        }

        public static void SetStoryFlag(string flag, bool value = true)
        {
            if (string.IsNullOrWhiteSpace(flag)) return;

            if (value) _storyFlags.Add(flag.Trim());
            else _storyFlags.Remove(flag.Trim());
        }

        public static bool IsQuestCompleted(string questId)
        {
            return !string.IsNullOrEmpty(questId) && _completedQuests.Contains(questId);
        }

        public static void CompleteQuest(string questId)
        {
            if (!string.IsNullOrEmpty(questId)) _completedQuests.Add(questId);
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
