using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Tanks2D
{
    // Деревня после уровня: панорама города (по мотивам городов Героев 3) с кликабельными зданиями.
    // Здание открывает окно со списком жителей-квестгиверов, житель — разговор с героем.
    // Всё строится из данных текущего города (TownDefinition), поэтому новые здания и жители
    // добавляются в ассете города без правки сцены.
    public class VillageController : MonoBehaviour
    {
        [Header("Город")]
        [SerializeField] private TextMeshProUGUI _townName;
        [SerializeField] private TextMeshProUGUI _infoText;
        [SerializeField] private ScrollRect _townView;
        [SerializeField] private RectTransform _content;
        [SerializeField] private Image _sky;
        [SerializeField] private Image _ground;
        [Tooltip("Шаблон кнопки здания: Image + дочерний TextMeshProUGUI с названием")]
        [SerializeField] private Button _buildingTemplate;

        [Header("Окно здания")]
        [SerializeField] private GameObject _buildingPanel;
        [SerializeField] private TextMeshProUGUI _buildingTitle;
        [SerializeField] private TextMeshProUGUI _buildingDescription;
        [SerializeField] private Transform _npcContainer;
        [Tooltip("Шаблон кнопки жителя: Image-портрет (дочерний Portrait) + TextMeshProUGUI с именем")]
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
        private readonly List<Button> _npcButtons = new List<Button>();
        private TownDefinition _town;
        private BuildingDefinition _openBuilding;
        private float _lastViewportHeight = -1f;

        public TownDefinition Town => _town;
        public IReadOnlyList<Button> BuildingButtons => _buildingButtons;
        public BuildingDefinition OpenBuildingData => _openBuilding;
        public bool IsBuildingOpen => _buildingPanel != null && _buildingPanel.activeSelf;
        public int NpcCount => _npcButtons.Count;
        public bool LeadsToMap => CampaignRun.CanTravel;

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
            if (_infoText != null)
            {
                RunMap map = CampaignRun.Map;
                string levels = map != null ? $"Уровни {RunMap.FirstLevelOfRow(map.currentRow)}–{RunMap.LastLevelOfRow(map.currentRow)}. " : "";
                _infoText.text = levels + "Нажмите на здание, чтобы поговорить с жителями";
            }

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

                TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
                if (label != null) label.text = building.displayName;

                BuildingDefinition captured = building;
                button.onClick.AddListener(() => OpenBuilding(captured));
                _buildingButtons.Add(button);
            }
        }

        // Уровни города пройдены — на карту похода (и в дорогу), иначе — в лобби к следующему бою
        public void Continue()
        {
            GamePause.Clear();
            SceneManager.LoadScene(LeadsToMap ? _mapScene : _lobbyScene);
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
            if (_buildingDescription != null)
            {
                _buildingDescription.text = building.npcs.Count > 0
                    ? (string.IsNullOrEmpty(building.description) ? "Кто-то здесь хочет с вами поговорить." : building.description)
                    : "Здесь пока никого нет.";
            }

            foreach (Button old in _npcButtons) Destroy(old.gameObject);
            _npcButtons.Clear();

            foreach (NpcDefinition npc in building.npcs)
            {
                if (npc == null || _npcTemplate == null) continue;

                Button button = Instantiate(_npcTemplate, _npcContainer != null ? _npcContainer : _npcTemplate.transform.parent);
                button.name = $"Npc_{npc.id}";
                button.gameObject.SetActive(true);

                Transform portraitTransform = button.transform.Find("Portrait");
                Image portrait = portraitTransform != null ? portraitTransform.GetComponent<Image>() : null;
                if (portrait != null)
                {
                    portrait.sprite = npc.portrait != null ? npc.portrait : PlaceholderSprites.Get(PlaceholderShape.Circle);
                    portrait.color = npc.portrait != null ? Color.white : npc.placeholderColor;
                    portrait.preserveAspect = true;
                }

                TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
                if (label != null) label.text = npc.displayName;

                NpcDefinition captured = npc;
                button.onClick.AddListener(() => Talk(captured));
                _npcButtons.Add(button);
            }

            _buildingPanel.SetActive(true);
        }

        public void CloseBuilding()
        {
            if (_buildingPanel != null) _buildingPanel.SetActive(false);
            _openBuilding = null;
        }

        // ---------------------------------------------------------------- Разговор

        public void Talk(int npcIndex)
        {
            if (_openBuilding != null && npcIndex >= 0 && npcIndex < _openBuilding.npcs.Count) Talk(_openBuilding.npcs[npcIndex]);
        }

        public void Talk(NpcDefinition npc)
        {
            if (npc == null || _dialogue == null) return;
            _dialogue.Play(npc.dialogue.Count > 0 ? npc.dialogue : DefaultDialogue(npc), null);
        }

        // Пока все разговоры одинаковые; задания появятся позже (npc.quests)
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
