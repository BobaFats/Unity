using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Tanks2D
{
    // Задание: сцена-разговор у жителя города, условия появления и последствия.
    // Задания связаны между собой не напрямую, а через флаги мира (GameStats.StoryFlags):
    // одно задание выставляет флаг («торгаш похищен»), другое на него смотрит.
    // Создать: Create > Shooter > Quest, затем добавить в Assets/Resources/QuestCatalog.
    [CreateAssetMenu(fileName = "Quest", menuName = "Shooter/Quest")]
    public class QuestDefinition : ScriptableObject
    {
        [Tooltip("Уникальный ключ задания. Пусто — имя ассета")]
        [SerializeField] private string _id;
        public string title = "Задание";
        [Tooltip("Заметка для сценариста: о чём задание. В игре не показывается")]
        [TextArea] public string description;

        [Header("Где появляется")]
        [Tooltip("Город. Пусто — в любом городе")]
        public TownDefinition town;
        [Tooltip("id здания на панораме города: castle, mage_guild, tavern, blacksmith, market, town_hall, stables, barracks, temple, harbor")]
        public string buildingId = "market";

        [Header("Когда появляется (флаги мира)")]
        [Tooltip("Все эти флаги должны быть выставлены прошлыми заданиями. Пусто — без условий")]
        public List<string> requiredFlags = new List<string>();
        [Tooltip("Если выставлен хотя бы один из этих флагов, задание не появится")]
        public List<string> blockingFlags = new List<string>();

        [Header("Кто выдаёт")]
        [Tooltip("id жителя этого здания (trader, smith...) — задание выдаёт он. Пусто — у задания свой житель (ниже): он появляется в здании вместе с заданием и исчезает после него")]
        public string giverNpcId;
        public string giverName = "Житель";
        [Tooltip("Портрет своего жителя. Пусто — заглушка")]
        public Sprite giverPortrait;
        public Color giverColor = new Color(0.8f, 0.7f, 0.55f);

        [Header("Сцена задания: реплики и титры по порядку")]
        public List<DialogueLine> lines = new List<DialogueLine>();

        [Header("Итог")]
        [Tooltip("Текст на карточке «Задание выполнено»")]
        [TextArea] public string resultText;
        [Tooltip("Флаги, которые задание выставляет (на них смотрят следующие задания)")]
        public List<string> setFlags = new List<string>();
        [Tooltip("Флаги, которые задание снимает")]
        public List<string> clearFlags = new List<string>();
        [Tooltip("Награда или цена: опыт, подковы, стена, башня... (те же исходы, что у встреч в дороге)")]
        public List<EncounterEffect> effects = new List<EncounterEffect>();

        public string Id => string.IsNullOrEmpty(_id) ? name : _id;

        // Задание ещё не выполнено, город подходит, условия по флагам сошлись
        public bool IsAvailableIn(TownDefinition current)
        {
            if (GameStats.IsQuestCompleted(Id)) return false;
            if (town != null && (current == null || town.Id != current.Id)) return false;

            foreach (string flag in requiredFlags)
            {
                if (!string.IsNullOrWhiteSpace(flag) && !GameStats.HasStoryFlag(flag)) return false;
            }
            foreach (string flag in blockingFlags)
            {
                if (GameStats.HasStoryFlag(flag)) return false;
            }
            return true;
        }

        // Реплики для показа. Своему жителю подставляется его портрет (сам ассет не меняется)
        public List<DialogueLine> BuildLines()
        {
            var result = new List<DialogueLine>(lines.Count);
            foreach (DialogueLine line in lines)
            {
                if (line == null) continue;

                bool byGiver = giverPortrait != null && line.portraitSprite == null && !line.IsNarration && line.speaker == giverName;
                if (byGiver)
                {
                    DialogueLine copy = line.Clone();
                    copy.portraitSprite = giverPortrait;
                    result.Add(copy);
                }
                else result.Add(line);
            }
            return result;
        }

        // Последствия: задание засчитано, флаги мира и награды применены
        public void Complete()
        {
            GameStats.CompleteQuest(Id);
            foreach (string flag in setFlags) GameStats.SetStoryFlag(flag, true);
            foreach (string flag in clearFlags) GameStats.SetStoryFlag(flag, false);
            EncounterEffects.Apply(effects);
        }

        // Текст карточки «Задание выполнено»
        public string ResultCardText()
        {
            var text = new StringBuilder();
            text.Append("<b>Задание выполнено</b>\n«").Append(title).Append('»');
            if (!string.IsNullOrEmpty(resultText)) text.Append("\n\n").Append(resultText);

            string rewards = EncounterEffects.Describe(effects);
            if (!string.IsNullOrEmpty(rewards)) text.Append("\n\n").Append(rewards);
            return text.ToString();
        }
    }
}
