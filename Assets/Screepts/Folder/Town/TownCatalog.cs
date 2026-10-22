using System.Collections.Generic;
using UnityEngine;

namespace Tanks2D
{
    // Все города кампании (Assets/Resources/TownCatalog.asset).
    // Текущий город: выбранный игроком (GameStats.CurrentTownId) или тот, к которому относится текущий уровень.
    [CreateAssetMenu(fileName = "TownCatalog", menuName = "Shooter/Town Catalog")]
    public class TownCatalog : ScriptableObject
    {
        public const string ResourcePath = "TownCatalog";

        [SerializeField] private List<TownDefinition> _towns = new List<TownDefinition>();

        private static TownCatalog _instance;

        public static TownCatalog Instance
        {
            get
            {
                if (_instance == null) _instance = Resources.Load<TownCatalog>(ResourcePath);
                return _instance;
            }
        }

        public static void SetInstance(TownCatalog catalog) => _instance = catalog;

        public IReadOnlyList<TownDefinition> Towns => _towns;

        public TownDefinition Find(string id)
        {
            foreach (TownDefinition town in _towns)
            {
                if (town != null && town.Id == id) return town;
            }
            return null;
        }

        public TownDefinition ForLevel(int level)
        {
            foreach (TownDefinition town in _towns)
            {
                if (town != null && town.ContainsLevel(level)) return town;
            }
            return _towns.Count > 0 ? _towns[_towns.Count - 1] : null;
        }

        // Город, где сейчас находится герой. Победа на уровне — деревня города этого уровня.
        public static TownDefinition Current
        {
            get
            {
                TownCatalog catalog = Instance;
                if (catalog == null) return null;

                // Есть карта забега — город там, где сейчас герой
                TownDefinition onMap = CampaignRun.CurrentTown;
                if (onMap != null) return onMap;

                // Последний пройденный уровень (после победы номер уровня уже увеличен)
                int completed = GameStats.CampaignCompleted ? GameStats.Mission : Mathf.Max(1, GameStats.Mission - 1);

                TownDefinition chosen = string.IsNullOrEmpty(GameStats.CurrentTownId) ? null : catalog.Find(GameStats.CurrentTownId);
                return chosen != null && chosen.ContainsLevel(completed) ? chosen : catalog.ForLevel(completed);
            }
        }

        public void SetTowns(List<TownDefinition> towns) => _towns = towns;
    }
}
