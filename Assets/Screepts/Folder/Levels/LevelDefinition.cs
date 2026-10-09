using System;
using System.Collections.Generic;
using UnityEngine;

namespace Tanks2D
{
    // Способности босса / мини-босса. Флаги: можно включать несколько сразу.
    // Новая способность = новый флаг (следующая степень двойки) + её реализация в BossAbilities.
    [Flags]
    public enum BossAbility
    {
        None = 0,
        Shield = 1 << 0,         // щит со своей полоской прочности, пока цел — урон по боссу не проходит
        RamWall = 1 << 1,        // таран стены и откат назад
        DestroyTowers = 1 << 2   // разрушает башни-помощники
    }

    // Мини-босс: появляется по ходу уровня после заданного числа убийств
    [Serializable]
    public class MiniBossSpawn
    {
        [Tooltip("Префаб мини-босса (любой префаб врага с PigEnemy)")]
        public GameObject prefab;
        [Tooltip("После скольких убийств обычных врагов появляется")]
        [Min(1)] public int afterKills = 20;
        [Tooltip("Способности мини-босса (нужен компонент BossAbilities на префабе)")]
        public BossAbility abilities = BossAbility.None;
        [Tooltip("Множитель здоровья относительно префаба")]
        [Min(0.1f)] public float healthMultiplier = 1f;
    }

    // Один уровень кампании (Assets/Resources/Levels/Level_01..Level_30).
    // Пустые поля означают «как в сцене/спавнере по умолчанию».
    [CreateAssetMenu(fileName = "Level_00", menuName = "Shooter/Level Definition")]
    public class LevelDefinition : ScriptableObject
    {
        [Min(1)] public int levelNumber = 1;
        public string title = "Уровень";
        [TextArea] public string description;

        [Header("Ход уровня")]
        [Tooltip("Сколько обычных врагов убить до появления босса. 0 — как в спавнере")]
        [Min(0)] public int killsForBoss = 60;
        [Tooltip("Множитель здоровья всех врагов уровня (поверх роста по времени и уровню героя)")]
        [Min(0.1f)] public float enemyHealthMultiplier = 1f;
        [Tooltip("Свои враги уровня. Пусто — враги из спавнера сцены")]
        public List<EnemySpawner2D.EnemySpawnConfig> enemies = new List<EnemySpawner2D.EnemySpawnConfig>();

        [Header("Босс")]
        [Tooltip("Префаб босса. Пусто — босс из спавнера сцены")]
        public GameObject bossPrefab;
        public BossAbility bossAbilities = BossAbility.Shield;
        [Min(0.1f)] public float bossHealthMultiplier = 1f;
        [Tooltip("Реплики при появлении босса. Пусто — реплики из спавнера сцены")]
        public List<DialogueLine> bossIntroDialogue = new List<DialogueLine>();

        [Header("Мини-боссы")]
        public List<MiniBossSpawn> miniBosses = new List<MiniBossSpawn>();

        public string DisplayName => string.IsNullOrEmpty(title) ? $"Уровень {levelNumber}" : $"Уровень {levelNumber}. {title}";

        // Короткое описание способностей для лобби
        public static string DescribeAbilities(BossAbility abilities)
        {
            if (abilities == BossAbility.None) return "без особых способностей";

            var parts = new List<string>();
            if ((abilities & BossAbility.Shield) != 0) parts.Add("щит");
            if ((abilities & BossAbility.RamWall) != 0) parts.Add("таран стены");
            if ((abilities & BossAbility.DestroyTowers) != 0) parts.Add("разрушает башни");
            return string.Join(", ", parts);
        }
    }
}
