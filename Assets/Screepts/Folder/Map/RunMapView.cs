using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Tanks2D
{
    // Экран карты забега (по мотивам Slay the Spire): ряды городов снизу вверх, дороги между ними.
    // Видно пройденный путь, текущий город и куда можно поехать. Нажатие на доступный город намечает маршрут —
    // окончательно путь выбирается на развилке в конце поездки на дилижансе.
    public class RunMapView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _title;
        [SerializeField] private TextMeshProUGUI _info;
        [SerializeField] private ScrollRect _scroll;
        [SerializeField] private RectTransform _content;
        [Tooltip("Шаблон города: Image + дочерний TextMeshProUGUI")]
        [SerializeField] private Button _nodeTemplate;
        [Tooltip("Шаблон дороги: Image")]
        [SerializeField] private Image _edgeTemplate;
        [SerializeField] private Button _travelButton;
        [SerializeField] private string _travelScene = "Stagecoach";
        [SerializeField] private string _menuScene = "Menu";

        [Header("Вид")]
        [SerializeField] private float _rowSpacing = 320f;
        [SerializeField] private float _sidePadding = 170f;
        [SerializeField] private float _nodeSize = 150f;
        [SerializeField] private Color _visitedColor = new Color(0.45f, 0.75f, 0.45f);
        [SerializeField] private Color _currentColor = new Color(1f, 0.82f, 0.25f);
        [SerializeField] private Color _optionColor = new Color(0.95f, 0.95f, 0.95f);
        [SerializeField] private Color _plannedColor = new Color(0.35f, 0.85f, 1f);
        [SerializeField] private Color _lockedColor = new Color(0.4f, 0.4f, 0.45f);

        private readonly Dictionary<Vector2Int, Button> _nodes = new Dictionary<Vector2Int, Button>();
        private readonly List<Image> _edges = new List<Image>();
        private RunMap _map;

        public RunMap Map => _map;
        public int NodeCount => _nodes.Count;
        public int EdgeCount => _edges.Count;

        private void Start()
        {
            GamePause.Clear();
            if (_nodeTemplate != null) _nodeTemplate.gameObject.SetActive(false);
            if (_edgeTemplate != null) _edgeTemplate.gameObject.SetActive(false);
            if (_travelButton != null) _travelButton.onClick.AddListener(Travel);

            _map = CampaignRun.Map;
            Build();
            Refresh();
            SaveSystem.SaveRun("Map");
        }

        private void Build()
        {
            if (_map == null || _content == null) return;

            Canvas.ForceUpdateCanvases();
            float width = _content.rect.width;
            _content.sizeDelta = new Vector2(_content.sizeDelta.x, _map.RowCount * _rowSpacing + _rowSpacing * 0.5f);

            // Сначала дороги (под городами)
            for (int r = 0; r < _map.RowCount - 1; r++)
            {
                foreach (MapNode node in _map.rows[r].nodes)
                {
                    foreach (int next in node.next)
                    {
                        Image edge = Instantiate(_edgeTemplate, _content);
                        edge.gameObject.SetActive(true);
                        PlaceEdge((RectTransform)edge.transform, NodePosition(r, node.index, width), NodePosition(r + 1, next, width));
                        edge.name = $"Road_{r}_{node.index}_{next}";
                        _edges.Add(edge);
                    }
                }
            }

            for (int r = 0; r < _map.RowCount; r++)
            {
                foreach (MapNode node in _map.rows[r].nodes)
                {
                    Button button = Instantiate(_nodeTemplate, _content);
                    button.gameObject.SetActive(true);
                    button.name = $"Town_{r}_{node.index}";

                    var rect = (RectTransform)button.transform;
                    rect.anchorMin = rect.anchorMax = Vector2.zero;
                    rect.pivot = new Vector2(0.5f, 0.5f);
                    rect.sizeDelta = new Vector2(_nodeSize, _nodeSize);
                    rect.anchoredPosition = NodePosition(r, node.index, width);

                    Image image = button.GetComponent<Image>();
                    image.sprite = PlaceholderSprites.Get(r == _map.RowCount - 1 ? PlaceholderShape.Diamond : PlaceholderShape.Circle);
                    image.type = Image.Type.Simple;

                    TownDefinition town = CampaignRun.TownOf(node);
                    TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
                    if (label != null) label.text = town != null ? town.displayName : "?";

                    MapNode captured = node;
                    button.onClick.AddListener(() => Plan(captured));
                    _nodes[new Vector2Int(r, node.index)] = button;
                }
            }

            // Прокручиваем к текущему городу
            if (_scroll != null) _scroll.verticalNormalizedPosition = _map.RowCount > 1 ? Mathf.Clamp01((_map.currentRow - 1f) / (_map.RowCount - 2f)) : 0f;
        }

        private Vector2 NodePosition(int row, int index, float width)
        {
            int count = _map.rows[row].nodes.Count;
            float t = count <= 1 ? 0.5f : index / (float)(count - 1);
            float x = Mathf.Lerp(_sidePadding, width - _sidePadding, count <= 1 ? 0.5f : t);
            float y = _rowSpacing * 0.5f + row * _rowSpacing;
            return new Vector2(x, y);
        }

        private static void PlaceEdge(RectTransform edge, Vector2 from, Vector2 to)
        {
            Vector2 delta = to - from;
            edge.anchorMin = edge.anchorMax = Vector2.zero;
            edge.pivot = new Vector2(0.5f, 0.5f);
            edge.anchoredPosition = (from + to) * 0.5f;
            edge.sizeDelta = new Vector2(delta.magnitude, 10f);
            edge.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }

        // ---------------------------------------------------------------- Состояние

        public void Plan(MapNode node)
        {
            if (_map == null || node == null || node.row != _map.currentRow + 1) return;
            if (_map.Current == null || !_map.Current.next.Contains(node.index)) return;

            _map.plannedIndex = node.index;
            Refresh();
            SaveSystem.SaveRun("Map");
        }

        private void Refresh()
        {
            if (_map == null)
            {
                if (_info != null) _info.text = "Карта не создана: нет городов в TownCatalog";
                return;
            }

            var options = new HashSet<int>();
            foreach (MapNode option in _map.NextOptions) options.Add(option.index);

            foreach (KeyValuePair<Vector2Int, Button> pair in _nodes)
            {
                Vector2Int key = pair.Key;
                Color color = _lockedColor;
                float scale = 1f;

                if (key.x == _map.currentRow && key.y == _map.currentIndex) { color = _currentColor; scale = 1.25f; }
                else if (_map.IsVisited(key.x, key.y)) color = _visitedColor;
                else if (key.x == _map.currentRow + 1 && options.Contains(key.y)) color = key.y == _map.plannedIndex ? _plannedColor : _optionColor;

                pair.Value.GetComponent<Image>().color = color;
                pair.Value.transform.localScale = Vector3.one * scale;
                pair.Value.interactable = key.x == _map.currentRow + 1 && options.Contains(key.y);
            }

            foreach (Image edge in _edges)
            {
                string[] parts = edge.name.Split('_');
                int row = int.Parse(parts[1]), from = int.Parse(parts[2]), to = int.Parse(parts[3]);
                bool walked = _map.IsVisited(row, from) && _map.IsVisited(row + 1, to);
                bool open = row == _map.currentRow && from == _map.currentIndex;
                edge.color = walked ? _visitedColor : open ? (to == _map.plannedIndex ? _plannedColor : _optionColor) : new Color(0.3f, 0.3f, 0.35f, 0.7f);
            }

            TownDefinition town = CampaignRun.CurrentTown;
            if (_title != null) _title.text = "Карта похода";

            if (_info != null)
            {
                string where = town != null ? town.displayName : "?";
                if (_map.IsFinalRow)
                {
                    _info.text = GameStats.CampaignCompleted ? $"Поход завершён в городе {where}!" : $"Вы в последнем городе: {where}";
                }
                else
                {
                    MapNode planned = _map.Get(_map.currentRow + 1, _map.plannedIndex);
                    TownDefinition plannedTown = CampaignRun.TownOf(planned);
                    _info.text = $"Вы в городе {where}. Дальше {options.Count} дороги.\n" +
                                 (plannedTown != null ? $"Намечен путь: {plannedTown.displayName}" : "Нажмите на город, чтобы наметить путь. Выбор — на развилке в дороге.");
                }
            }

            if (_travelButton != null)
            {
                TextMeshProUGUI label = _travelButton.GetComponentInChildren<TextMeshProUGUI>(true);
                if (label != null) label.text = CampaignRun.CanTravel ? "Сесть в дилижанс" : "В главное меню";
            }
        }

        public void Travel()
        {
            GamePause.Clear();
            SceneManager.LoadScene(CampaignRun.CanTravel ? _travelScene : _menuScene);
        }
    }
}
