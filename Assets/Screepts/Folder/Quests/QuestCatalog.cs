using System.Collections.Generic;
using UnityEngine;

namespace Tanks2D
{
    // Все задания игры (Assets/Resources/QuestCatalog.asset).
    // Какие из них доступны прямо сейчас, решают сами задания: город, флаги мира, выполнено ли уже.
    [CreateAssetMenu(fileName = "QuestCatalog", menuName = "Shooter/Quest Catalog")]
    public class QuestCatalog : ScriptableObject
    {
        public const string ResourcePath = "QuestCatalog";

        [SerializeField] private List<QuestDefinition> _quests = new List<QuestDefinition>();

        private static QuestCatalog _instance;

        public static QuestCatalog Instance
        {
            get
            {
                if (_instance == null) _instance = Resources.Load<QuestCatalog>(ResourcePath);
                return _instance;
            }
        }

        public static void SetInstance(QuestCatalog catalog) => _instance = catalog;

        public IReadOnlyList<QuestDefinition> Quests => _quests;

        public QuestDefinition Find(string id)
        {
            foreach (QuestDefinition quest in _quests)
            {
                if (quest != null && quest.Id == id) return quest;
            }
            return null;
        }

        // Задания, которые сейчас можно получить в этом городе
        public void CollectAvailable(TownDefinition town, List<QuestDefinition> result)
        {
            foreach (QuestDefinition quest in _quests)
            {
                if (quest != null && quest.IsAvailableIn(town)) result.Add(quest);
            }
        }

        public void Add(QuestDefinition quest)
        {
            if (quest != null && !_quests.Contains(quest)) _quests.Add(quest);
        }
    }
}
