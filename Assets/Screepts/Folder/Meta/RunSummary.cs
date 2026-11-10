using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Tanks2D
{
    // Итоги одного забега
    [Serializable]
    public class RunResult
    {
        public bool victory;
        public int levels;
        public int bosses;
        public int kills;
        public int towns;
        public int quests;
        public int defeats;
        public float playTime;
        public int glory;
        public bool newRecord;
    }

    // Конец забега: победа после последнего уровня или «Завершить поход».
    // Считает итоги и славу, отдаёт славу в наследие, удаляет сохранение забега и показывает экран итогов.
    public static class RunSummary
    {
        public const string RunEndSceneName = "RunEnd";

        // Слава за забег (баланс наследия крутить здесь)
        public const int GloryPerLevel = 10;
        public const int GloryPerQuest = 15;
        public const int GloryPerTown = 5;
        public const int GloryPer100Kills = 2;
        public const int GloryForVictory = 200;

        public static RunResult LastResult { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad() => LastResult = null;

        public static RunResult Calculate(bool victory)
        {
            RunMap map = GameStats.Map;
            var result = new RunResult
            {
                victory = victory,
                levels = CampaignRun.CompletedLevels,
                bosses = GameStats.BossesDefeated,
                kills = GameStats.RunKills,
                towns = map != null ? map.visited.Count : 1,
                quests = GameStats.CompletedQuests.Count,
                defeats = GameStats.RunDefeats,
                playTime = GameStats.RunPlayTime
            };

            result.glory = result.levels * GloryPerLevel
                           + result.quests * GloryPerQuest
                           + result.towns * GloryPerTown
                           + result.kills / 100 * GloryPer100Kills
                           + (victory ? GloryForVictory : 0);
            return result;
        }

        public static void Finish(bool victory, string sceneName = RunEndSceneName)
        {
            RunResult result = Calculate(victory);
            result.newRecord = MetaProgress.AddRun(result);
            LastResult = result;

            // Забег окончен: «Продолжить» в меню больше не предлагается
            SaveSystem.DeleteRun();
            GameStats.InBattle = false;

            GamePause.Clear();
            SceneManager.LoadScene(string.IsNullOrEmpty(sceneName) ? RunEndSceneName : sceneName);
        }

        public static string FormatTime(float seconds)
        {
            var time = TimeSpan.FromSeconds(Mathf.Max(0f, seconds));
            return time.TotalHours >= 1 ? $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}" : $"{time.Minutes}:{time.Seconds:00}";
        }
    }
}
