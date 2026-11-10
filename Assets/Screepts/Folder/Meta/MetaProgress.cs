using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Tanks2D
{
    // Наследие: то, что остаётся между забегами — слава, купленные улучшения, рекорды.
    // Хранится в Application.persistentDataPath/meta.json (отдельно от забега).
    public static class MetaProgress
    {
        [Serializable]
        private class Data
        {
            public int glory;
            public int totalGlory;
            public int runsFinished;
            public int victories;
            public int bestLevels;
            public int bestKills;
            public List<string> upgradeIds = new List<string>();
            public List<int> upgradeLevels = new List<int>();
        }

        private static Data _data;

        private static Data Current
        {
            get
            {
                if (_data == null) _data = Load();
                return _data;
            }
        }

        public static int Glory => Current.glory;
        public static int TotalGlory => Current.totalGlory;
        public static int RunsFinished => Current.runsFinished;
        public static int Victories => Current.victories;
        public static int BestLevels => Current.bestLevels;
        public static int BestKills => Current.bestKills;

        // Перечитать с диска (после смены папки сохранений в тестах или при запуске)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reload() => _data = null;

        public static int LevelOf(string upgradeId)
        {
            int index = Current.upgradeIds.IndexOf(upgradeId);
            return index >= 0 && index < Current.upgradeLevels.Count ? Current.upgradeLevels[index] : 0;
        }

        public static bool CanBuy(MetaUpgrade upgrade)
        {
            if (upgrade == null) return false;
            int level = LevelOf(upgrade.id);
            return level < upgrade.maxLevel && Glory >= upgrade.CostOf(level);
        }

        public static bool Buy(MetaUpgrade upgrade)
        {
            if (!CanBuy(upgrade)) return false;

            int level = LevelOf(upgrade.id);
            Current.glory -= upgrade.CostOf(level);

            int index = Current.upgradeIds.IndexOf(upgrade.id);
            if (index < 0)
            {
                Current.upgradeIds.Add(upgrade.id);
                Current.upgradeLevels.Add(level + 1);
            }
            else Current.upgradeLevels[index] = level + 1;

            Save();
            return true;
        }

        // Итоги забега: слава и рекорды. Возвращает true, если побит рекорд по пройденным уровням
        public static bool AddRun(RunResult result)
        {
            Current.glory += result.glory;
            Current.totalGlory += result.glory;
            Current.runsFinished++;
            if (result.victory) Current.victories++;
            Current.bestKills = Mathf.Max(Current.bestKills, result.kills);

            bool record = result.levels > Current.bestLevels;
            Current.bestLevels = Mathf.Max(Current.bestLevels, result.levels);

            Save();
            return record;
        }

        // Для тестов и отладки: начислить славу
        public static void AddGlory(int amount)
        {
            Current.glory = Mathf.Max(0, Current.glory + amount);
            Save();
        }

        private static Data Load()
        {
            try
            {
                if (File.Exists(SaveSystem.MetaPath)) return JsonUtility.FromJson<Data>(File.ReadAllText(SaveSystem.MetaPath)) ?? new Data();
            }
            catch (Exception exception)
            {
                Debug.LogError($"[Наследие] Файл испорчен, начинаем заново: {exception.Message}");
            }
            return new Data();
        }

        private static void Save()
        {
            try
            {
                SaveSystem.WriteAtomic(SaveSystem.MetaPath, JsonUtility.ToJson(Current, true));
            }
            catch (Exception exception)
            {
                Debug.LogError($"[Наследие] Не удалось сохранить: {exception.Message}");
            }
        }
    }
}
