using UnityEngine;

namespace Tanks2D
{
    // Текущий забег: карта городов, положение героя на ней и то, что везётся между боями.
    public static class CampaignRun
    {
        // Карта создаётся при первом обращении в новой игре (своя для каждого забега)
        public static RunMap Map
        {
            get
            {
                if (GameStats.Map == null && TownCatalog.Instance != null && TownCatalog.Instance.Towns.Count > 0)
                {
                    GameStats.Map = RunMapGenerator.Generate(Random.Range(1, int.MaxValue), TownCatalog.Instance.Towns);
                }
                return GameStats.Map;
            }
        }

        public static TownDefinition CurrentTown
        {
            get
            {
                MapNode node = Map?.Current;
                return node != null && TownCatalog.Instance != null ? TownCatalog.Instance.Find(node.townId) : null;
            }
        }

        public static TownDefinition TownOf(MapNode node)
        {
            return node != null && TownCatalog.Instance != null ? TownCatalog.Instance.Find(node.townId) : null;
        }

        // Сколько уровней уже пройдено
        public static int CompletedLevels => GameStats.CampaignCompleted ? GameStats.TotalLevels : GameStats.Mission - 1;

        // Все уровни текущего города пройдены — пора в дорогу
        public static bool TownLevelsDone
        {
            get
            {
                RunMap map = Map;
                return map != null && CompletedLevels >= RunMap.LastLevelOfRow(map.currentRow);
            }
        }

        public static bool CanTravel => TownLevelsDone && Map != null && !Map.IsFinalRow;
    }
}
