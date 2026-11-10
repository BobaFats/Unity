using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Tanks2D
{
    // Деревня после уровня: панорама города (по мотивам городов Героев 3) с кликабельными зданиями.
    // Здание открывает окно со списком жителей-квестгиверов, житель — разговор с героем.
    // Всё строится из данных текущего города (TownDefinition), поэтому новые здания и жители
    // добавляются в ассете города без правки сцены.
    //
    // Задания берутся из QuestCatalog. Над зданием, где сейчас можно получить задание, горит метка «!»,
    // внутри метка стоит у жителя, который его выдаёт. Разговор с ним проигрывает сцену задания,
    // затем применяются последствия (флаги мира, награды) и показывается карточка «Задание выполнено».
    public class VillageController : MonoBehaviour
    {
        // Дочерний объект-метка в шаблонах здания и жителя: включается там, где есть задание
        public const string QuestMarkerName = "QuestMarker";

        [Header("Город")]
        [SerializeField] private TextMeshProUGUI _townName;
        [SerializeField] private TextMeshProUGUI _infoText;
        [SerializeField] private ScrollRect _townView;
        [SerializeField] private RectTransform _content;
        [SerializeField] private Image _sky;
        [SerializeField] private Image _ground;
        [Tooltip("Шаблон кнопки здания: Image + дочерний TextMeshProUGUI с названием (+ метка задания QuestMarker)")]
        [SerializeField] private Button _buildingTemplate;

        [Header("Окно здания")]
        [SerializeField] private GameObject _buildingPanel;
        [SerializeField] private TextMeshProUGUI _buildingTitle;
        [SerializeField] private TextMeshProUGUI _buildingDescription;
        [SerializeField] private Transform _npcContainer;
        [Tooltip("Шаблон кнопки жителя: Image-портрет (дочерний Portrait) + TextMeshProUGUI с именем (+ метка задания QuestMarker)")]
        [SerializeField] private Button _npcTemplate;
        [SerializeField] private Button _closeBuildingButton;

        [Header("Разговор")]
        [SerializeField] private DialoguePanel _dialogue;

        [Header("Дальше")]
        [Tooltip("Кнопка «Продолжить»: в дорогу (карта похода), если уровни города пройдены, иначе — в лобби к следующему бою")]
        [SerializeField] private Button _continueButton;
        [SerializeField] private string _mapScene = "Map";
        [SerializeField] private string _lobbyScene = "Lobby";

        private readonly List<Button> _buildingButtons = new List<Button>();
        private readonly List<BuildingDefinition> _buildings = new List<BuildingDefinition>();
        private readonly List<Button> _npcButtons = new List<Button>();
        private readonly List<QuestDefinition> _quests = new List<QuestDefinition>();
        private readonly List<QuestDefinition> _questGivers = new List<QuestDefinition>();
        private TownDefinition _town;
        private BuildingDefinition _openBuilding;
        private float _lastViewportHeight = -1f;

        public TownDefinition Town => _town;
        public IReadOnlyList<Button> BuildingButtons => _buildingButtons;
        public BuildingDefinition OpenBuildingData => _openBuilding;
        public bool IsBuildingOpen => _buildingPanel != null && _buildingPanel.activeSelf;
        public int NpcCount => _npcButtons.Count;
        public bool LeadsToMap => CampaignRun.CanTravel;

        // Задания, которые можно получить в этом городе прямо сейчас
        public IReadOnlyList<QuestDefinition> AvailableQuests => _quests;
        // Задания со своим жителем: он пришёл в открытое здание ради разговора с героем
        public IReadOnlyList<QuestDefinition> QuestGivers => _questGivers;
        public QuestDefinition ActiveQuest { get; private set; }
        public QuestDefinition LastCompletedQuest { get; private set; }

        private void Start()
        {
            GamePause.Clear();

            if (_buildingTemplate != null) _buildingTemplate.gameObject.SetActive(false);
            if (_npcTemplate != null) _npcTemplate.gameObject.SetActive(false);
            if (_buildingPanel != null) _buildingPanel.SetActive(false);
            if (_closeBuildingButton != null) _closeBuildingButton.onClick.AddListener(CloseBuilding);
            if (_continueButton != null)
            {
                _continueButton.onClick.AddListener(Continue);
                TextMeshProUGUI label = _continueButton.GetComponentInChildren<TextMeshProUGUI>(true);
                if (label != null) label.text = LeadsToMap ? "Отправиться в путь" : "К следующему бою";
            }

            _town = TownCatalog.Current;
            if (_town != null) GameStats.CurrentTownId = _town.Id;

            BuildTown();
            SaveSystem.SaveRun("Village");
        }

        private void LateUpdate()
        {
            // Панорама по высоте окна просмотра, ширина — по пропорциям города
            if (_townView == null || _content == null || _town == null) return;

            float height = ((RectTransform)_townView.viewport).rect.height;
            if (Mathf.Approximately(height, _lastViewportHeight)) return;

            _lastViewportHeight = height;
            _content.sizeDelta = new Vector2(height * _town.panoramaAspect, _content.sizeDelta.y);
        }

        private void BuildTown()
        {
            if (_town == null)
            {
                if (_townName != null) _townName.text = "Город не настроен";
                if (_infoText != null) _infoText.text = "Добавьте города в Assets/Resources/TownCatalog";
                return;
            }

            if (_townName != null) _townName.text = _town.displayName;

            if (_sky != null)
            {
                _sky.sprite = _town.background;
                _sky.color = _town.background != null ? Color.white : _town.skyColor;
            }
            if (_ground != null)
            {
                _ground.gameObject.SetActive(_town.background == null);
                _ground.color = _town.groundColor;
            }

            foreach (BuildingDefinition building in _town.buildings)
            {
                if (building == null || _buildingTemplate == null) continue;

                Button button = Instantiate(_buildingTemplate, _content);
                button.name = $"Building_{building.id}";
                button.gameObject.SetActive(true);

                // Место на панораме: доли от левого верхнего угла -> якоря UI (от левого нижнего)
                var rect = (RectTransform)button.transform;
                Rect area = building.area;
                rect.anchorMin = new Vector2(area.xMin, 1f - area.yMax);
                rect.anchorMax = new Vector2(area.xMax, 1f - area.yMin);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;

                Image image = button.GetComponent<Image>();
                image.sprite = building.sprite != null ? building.sprite : PlaceholderSprites.Get(building.placeholderShape);
                image.color = building.sprite != null ? Color.white : building.placeholderColor;
                image.type = Image.Type.Simple;
                image.preserveAspect = building.sprite != null;

                TextMeshProUGUI label = FindLabel(button);
                if (label != null) label.text = building.displayName;

                BuildingDefinition captured = building;
                button.onClick.AddListener(() => OpenBuilding(captured));
                _buildingButtons.Add(button);
                _buildings.Add(building);
            }

            RefreshQuests();
        }

        // Уровни города пройдены — на карту похода (и в дорогу), иначе — в лобби к следующему бою
        public void Continue()
        {
            GamePause.Clear();
            SceneManager.LoadScene(LeadsToMap ? _mapScene : _lobbyScene);
        }

        // ---------------------------------------------------------------- Задания

        // Пересчитать доступные задания и метки «!» над зданиями
        private void RefreshQuests()
        {
            _quests.Clear();
            if (_town == null) return;

            QuestCatalog catalog = QuestCatalog.Instance;
            if (catalog != null) catalog.CollectAvailable(_town, _quests);

            // Задание, здания которого в этом городе нет, получить негде — подсказываем сценаристу
            for (int i = _quests.Count - 1; i >= 0; i--)
            {
                QuestDefinition quest = _quests[i];
                if (_buildings.Exists(b => b.id == quest.buildingId)) continue;

                Debug.LogWarning($"[Задания] «{quest.title}»: в городе «{_town.displayName}» нет здания с id «{quest.buildingId}»", quest);
                _quests.RemoveAt(i);
            }

            for (int i = 0; i < _buildingButtons.Count; i++) SetQuestMarker(_buildingButtons[i], HasQuest(_buildings[i]));

            if (_infoText != null)
            {
                RunMap map = CampaignRun.Map;
                string levels = map != null ? $"Уровни {RunMap.FirstLevelOfRow(map.currentRow)}–{RunMap.LastLevelOfRow(map.currentRow)}. " : "";
                _infoText.text = levels + (_quests.Count > 0 ? "Вас ждут там, где горит «!»" : "Нажмите на здание, чтобы поговорить с жителями");
            }
        }

        public bool HasQuest(BuildingDefinition building)
        {
            return building != null && _quests.Exists(q => q.buildingId == building.id);
        }

        // Задание, которое выдаёт этот житель здания (если такое сейчас есть)
        private QuestDefinition QuestOf(BuildingDefinition building, NpcDefinition npc)
        {
            return _quests.Find(q => q.buildingId == building.id && !string.IsNullOrEmpty(q.giverNpcId) && q.giverNpcId == npc.id);
        }

        private static NpcDefinition FindResident(BuildingDefinition building, string npcId)
        {
            return string.IsNullOrEmpty(npcId) ? null : building.npcs.Find(n => n != null && n.id == npcId);
        }

        public void PlayQuestGiver(int index)
        {
            if (index >= 0 && index < _questGivers.Count) PlayQuest(_questGivers[index]);
        }

        public void PlayQuest(QuestDefinition quest)
        {
            if (quest == null || _dialogue == null || _dialogue.IsOpen || ActiveQuest != null) return;
            if (!quest.IsAvailableIn(_town)) return;

            ActiveQuest = quest;
            _dialogue.Play(quest.BuildLines(), () => CompleteQuest(quest));
        }

        private void CompleteQuest(QuestDefinition quest)
        {
            ActiveQuest = null;
            LastCompletedQuest = quest;
            quest.Complete();
            SaveSystem.SaveRun("Village");

            // Мир изменился: метки и жители пересобираются (своего жителя задания в здании больше нет)
            RefreshQuests();
            if (IsBuildingOpen && _openBuilding != null) OpenBuilding(_openBuilding);

            _dialogue.Play(new[] { new DialogueLine { kind = DialogueLineKind.Narration, text = quest.ResultCardText() } }, null);
        }

        // ---------------------------------------------------------------- Здание

        public void OpenBuilding(int index)
        {
            if (_town != null && index >= 0 && index < _town.buildings.Count) OpenBuilding(_town.buildings[index]);
        }

        public void OpenBuilding(BuildingDefinition building)
        {
            if (building == null || _buildingPanel == null) return;
            if (_dialogue != null && _dialogue.IsOpen) return;

            _openBuilding = building;
            if (_buildingTitle != null) _buildingTitle.text = building.displayName;

            foreach (Button old in _npcButtons)
            {
                old.gameObject.SetActive(false);
                Destroy(old.gameObject);
            }
            _npcButtons.Clear();
            _questGivers.Clear();

            // Сначала жители, пришедшие со своим заданием, затем постоянные жители здания
            foreach (QuestDefinition quest in _quests)
            {
                if (quest.buildingId != building.id || FindResident(building, quest.giverNpcId) != null) continue;

                QuestDefinition captured = quest;
                AddNpcButton($"QuestGiver_{quest.Id}", quest.giverName, quest.giverPortrait, quest.giverColor, true, () => PlayQuest(captured));
                _questGivers.Add(quest);
            }

            foreach (NpcDefinition npc in building.npcs)
            {
                if (npc == null) continue;

                NpcDefinition captured = npc;
                AddNpcButton($"Npc_{npc.id}", npc.displayName, npc.portrait, npc.placeholderColor, QuestOf(building, npc) != null, () => Talk(captured));
            }

            if (_buildingDescription != null)
            {
                _buildingDescription.text = _npcButtons.Count > 0
                    ? (string.IsNullOrEmpty(building.description) ? "Кто-то здесь хочет с вами поговорить." : building.description)
                    : "Здесь пока никого нет.";
            }

            _buildingPanel.SetActive(true);
        }

        private void AddNpcButton(string objectName, string displayName, Sprite portraitSprite, Color placeholderColor, bool hasQuest, UnityAction onClick)
        {
            if (_npcTemplate == null) return;

            Button button = Instantiate(_npcTemplate, _npcContainer != null ? _npcContainer : _npcTemplate.transform.parent);
            button.name = objectName;
            button.gameObject.SetActive(true);

            Transform portraitTransform = button.transform.Find("Portrait");
            Image portrait = portraitTransform != null ? portraitTransform.GetComponent<Image>() : null;
            if (portrait != null)
            {
                portrait.sprite = portraitSprite != null ? portraitSprite : PlaceholderSprites.Get(PlaceholderShape.Circle);
                portrait.color = portraitSprite != null ? Color.white : placeholderColor;
                portrait.preserveAspect = true;
            }

            TextMeshProUGUI label = FindLabel(button);
            if (label != null) label.text = displayName;

            SetQuestMarker(button, hasQuest);
            button.onClick.AddListener(onClick);
            _npcButtons.Add(button);
        }

        public void CloseBuilding()
        {
            if (_buildingPanel != null) _buildingPanel.SetActive(false);
            _openBuilding = null;
        }

        // Подпись кнопки — первый текст, не относящийся к метке задания
        private static TextMeshProUGUI FindLabel(Component item)
        {
            foreach (TextMeshProUGUI text in item.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (text.transform.parent == null || text.transform.parent.name != QuestMarkerName) return text;
            }
            return null;
        }

        private static void SetQuestMarker(Component item, bool visible)
        {
            Transform marker = item.transform.Find(QuestMarkerName);
            if (marker != null) marker.gameObject.SetActive(visible);
        }

        // ---------------------------------------------------------------- Разговор

        public void Talk(int npcIndex)
        {
            if (_openBuilding != null && npcIndex >= 0 && npcIndex < _openBuilding.npcs.Count) Talk(_openBuilding.npcs[npcIndex]);
        }

        // У жителя есть задание — играем его сцену, иначе обычный разговор
        public void Talk(NpcDefinition npc)
        {
            if (npc == null || _dialogue == null) return;

            QuestDefinition quest = _openBuilding != null ? QuestOf(_openBuilding, npc) : null;
            if (quest != null)
            {
                PlayQuest(quest);
                return;
            }

            _dialogue.Play(npc.dialogue.Count > 0 ? npc.dialogue : DefaultDialogue(npc), null);
        }

        // Разговор жителя без задания и без своих реплик
        public static List<DialogueLine> DefaultDialogue(NpcDefinition npc)
        {
            return new List<DialogueLine>
            {
                new DialogueLine { speaker = npc.displayName, portrait = VisualId.Npc, portraitSprite = npc.portrait, text = "УУУУУ" },
                new DialogueLine { speaker = "Герой", portrait = VisualId.Player, text = "ААААА", rightSide = true },
                new DialogueLine { speaker = npc.displayName, portrait = VisualId.Npc, portraitSprite = npc.portrait, text = "Спасибо" }
            };
        }
    }
}
