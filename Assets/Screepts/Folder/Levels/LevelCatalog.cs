using System.Collections.Generic;
using UnityEngine;

namespace Tanks2D
{
    // Список уровней кампании по порядку (Assets/Resources/LevelCatalog.asset)
    [CreateAssetMenu(fileName = "LevelCatalog", menuName = "Shooter/Level Catalog")]
    public class LevelCatalog : ScriptableObject
    {
        public const string ResourcePath = "LevelCatalog";

        [SerializeField] private List<LevelDefinition> _levels = new List<LevelDefinition>();

        private static LevelCatalog _instance;

        public static LevelCatalog Instance
        {
            get
            {
                if (_instance == null) _instance = Resources.Load<LevelCatalog>(ResourcePath);
                return _instance;
            }
        }

        public static void SetInstance(LevelCatalog catalog) => _instance = catalog;

        public IReadOnlyList<LevelDefinition> Levels => _levels;
        public int Count => _levels.Count;

        // Уровень по номеру (1..Count). Номер больше последнего — последний уровень.
        public LevelDefinition Get(int levelNumber)
        {
            if (_levels.Count == 0) return null;
            int index = Mathf.Clamp(levelNumber - 1, 0, _levels.Count - 1);
            return _levels[index];
        }

        public static LevelDefinition Current => Instance != null ? Instance.Get(GameStats.Mission) : null;

        public void SetLevels(List<LevelDefinition> levels)
        {
            _levels = levels;
        }
    }
}
