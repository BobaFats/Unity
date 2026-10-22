using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Tanks2D
{
    // Поездка на дилижансе между городами — раннер на 3 полосы (дорога едет сверху вниз).
    //   Управление: свайп/стрелки — сменить полосу, тап/пробел/вверх — прыжок.
    //   Препятствия: бревно и яма (перепрыгнуть), камень и дерево (объехать), пролом моста (только прыжок).
    //   Местность сменяется: лес → поле → горы → река. Скорость растёт.
    //   Подковы — бонус. 3 колеса: столкновение отнимает колесо; без колёс дилижанс всё равно доезжает,
    //   но стена в следующем бою будет повреждена.
    //   В пути — встречи (карты как в Hand of Fate). В конце — развилка: полоса = город на карте забега.
    public class StagecoachRun : MonoBehaviour
    {
        public enum Biome { Forest, Field, Mountains, River }
        public enum RoadObjectType { Log, Hole, Rock, Tree, Gap, Horseshoe, Traveler, Signpost }

        private class RoadObject
        {
            public RoadObjectType type;
            public int lane;          // -1 — на все полосы
            public float distance;    // место на дороге
            public Transform view;
            public bool consumed;
            public int option = -1;   // для указателя развилки
        }

        [Header("Дорога")]
        [SerializeField] private float[] _laneX = { -2.6f, 0f, 2.6f };
        [SerializeField] private float _coachY = -5f;
        [Tooltip("Длина поездки в единицах дороги")]
        [SerializeField] private float _tripLength = 520f;
        [SerializeField] private float _startSpeed = 6f;
        [SerializeField] private float _maxSpeed = 11f;
        [Tooltip("На сколько вперёд видно дорогу (единиц)")]
        [SerializeField] private float _viewAhead = 22f;

        [Header("Прыжок и полосы")]
        [SerializeField] private float _jumpDuration = 0.65f;
        [SerializeField] private float _jumpScale = 1.35f;
        [SerializeField] private float _laneChangeSharpness = 18f;
        [Tooltip("Минимальный свайп в пикселях")]
        [SerializeField] private float _swipeThreshold = 60f;

        [Header("Препятствия и подковы")]
        [SerializeField] private float _minGap = 5f;
        [SerializeField] private float _maxGap = 8.5f;
        [SerializeField, Range(0f, 1f)] private float _horseshoeChance = 0.45f;

        [Header("Колёса")]
        [SerializeField] private int _maxWheels = 3;
        [Tooltip("Насколько повреждена стена в следующем бою, если дилижанс доехал без колёс")]
        [SerializeField, Range(0f, 1f)] private float _brokenCoachWallDamage = 0.3f;

        [Header("Встречи")]
        [SerializeField] private EncounterPanel _encounterPanel;
        [Tooltip("Где на пути встречаются путники (доли поездки)")]
        [SerializeField] private float[] _encounterPoints = { 0.3f, 0.65f };

        [Header("Сцены")]
        [SerializeField] private string _arrivalScene = "Village";

        [Header("Интерфейс")]
        [SerializeField] private TextMeshProUGUI _wheelsText;
        [SerializeField] private TextMeshProUGUI _horseshoesText;
        [SerializeField] private TextMeshProUGUI _biomeText;
        [SerializeField] private TextMeshProUGUI _hintText;
        [SerializeField] private Slider _progress;

        private readonly List<RoadObject> _objects = new List<RoadObject>();
        private readonly List<Transform> _dashes = new List<Transform>();
        private readonly List<MapNode> _options = new List<MapNode>();
        private readonly List<EncounterDefinition> _encounters = new List<EncounterDefinition>();

        private Transform _coach;
        private VisualSlot _coachVisual;
        private SpriteRenderer _road;
        private SpriteRenderer _leftSide;
        private SpriteRenderer _rightSide;
        private Vector3 _coachBaseScale = Vector3.one;

        private int _lane = 1;
        private float _distance;
        private float _airTime;
        private int _wheels;
        private bool _brokenDown;
        private float _nextRowDistance;
        private bool _finished;
        private float _crashFlash;
        private Vector2 _pressStart;
        private bool _pressActive;
        private bool _pressOverUI;

        public static StagecoachRun Instance { get; private set; }

        public int Lane => _lane;
        public float Distance => _distance;
        public float TripLength => _tripLength;
        public float Speed => Mathf.Lerp(_startSpeed, _maxSpeed, Mathf.Clamp01(_distance / _tripLength));
        public int Wheels => _wheels;
        public bool IsAirborne => _airTime > 0f;
        public bool IsBrokenDown => _brokenDown;
        public bool Finished => _finished;
        public Biome CurrentBiome => BiomeAt(_distance);
        public IReadOnlyList<MapNode> Options => _options;
        public EncounterPanel Encounters => _encounterPanel;
        public int ChosenOption { get; private set; } = -1;

        private void OnEnable()
        {
            Instance = this;
            EncounterEffects.WheelsChanged += AddWheels;
        }

        private void OnDisable()
        {
            EncounterEffects.WheelsChanged -= AddWheels;
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            GamePause.Clear();
            _wheels = _maxWheels;

            RunMap map = CampaignRun.Map;
            if (map != null) _options.AddRange(map.NextOptions);

            if (EncounterCatalog.Instance != null) _encounters.AddRange(EncounterCatalog.Instance.Draw(_encounterPoints.Length));

            BuildRoad();
            _nextRowDistance = _viewAhead * 0.6f;
            SpawnEncounters();
            SpawnFork();
            UpdateHud();
        }

        // ---------------------------------------------------------------- Игровой цикл

        private void Update()
        {
            if (_finished || GamePause.IsPaused) return;

            float dt = Time.deltaTime;
            _distance += Speed * dt;
            if (_airTime > 0f) _airTime = Mathf.Max(0f, _airTime - dt);

            HandleInput();
            SpawnAhead();
            UpdateObjects();
            UpdateCoach(dt);
            UpdateScenery(dt);
            UpdateHud();
        }

        public void ChangeLane(int direction)
        {
            if (_finished) return;
            _lane = Mathf.Clamp(_lane + direction, 0, _laneX.Length - 1);
        }

        public void Jump()
        {
            if (_finished || _airTime > 0f) return;
            _airTime = _jumpDuration;
        }

        public void AddWheels(int amount)
        {
            _wheels = Mathf.Clamp(_wheels + amount, 0, _maxWheels);
            if (_wheels > 0) _brokenDown = false;
            UpdateHud();
        }

        private void HandleInput()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame) ChangeLane(-1);
                if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame) ChangeLane(1);
                if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame) Jump();
            }

            // Свайп — полоса, короткий тап — прыжок
            Pointer pointer = Pointer.current;
            if (pointer == null) return;

            Vector2 position = pointer.position.ReadValue();
            if (pointer.press.wasPressedThisFrame)
            {
                _pressActive = true;
                _pressStart = position;
                _pressOverUI = PointerInput.IsOverUI(position);
            }

            if (_pressActive && pointer.press.wasReleasedThisFrame)
            {
                _pressActive = false;
                if (_pressOverUI) return;

                Vector2 delta = position - _pressStart;
                if (Mathf.Abs(delta.x) >= _swipeThreshold && Mathf.Abs(delta.x) > Mathf.Abs(delta.y)) ChangeLane(delta.x > 0f ? 1 : -1);
                else if (delta.y >= _swipeThreshold) Jump();
                else if (delta.magnitude < _swipeThreshold * 0.5f) Jump();
            }
        }

        // ---------------------------------------------------------------- Дорога и объекты

        private Biome BiomeAt(float distance)
        {
            float t = distance / _tripLength;
            if (t < 0.25f) return Biome.Forest;
            if (t < 0.5f) return Biome.Field;
            if (t < 0.75f) return Biome.Mountains;
            return Biome.River;
        }

        private bool InQuietZone(float distance)
        {
            // Без препятствий: в начале, у путников и перед развилкой
            if (distance < 12f || distance > _tripLength - 25f) return true;
            foreach (float point in _encounterPoints)
            {
                if (Mathf.Abs(distance - point * _tripLength) < 6f) return true;
            }
            return false;
        }

        private void SpawnAhead()
        {
            while (_nextRowDistance < _distance + _viewAhead && _nextRowDistance < _tripLength)
            {
                if (!InQuietZone(_nextRowDistance)) SpawnRow(_nextRowDistance);
                _nextRowDistance += Random.Range(_minGap, _maxGap) * Mathf.Lerp(1f, 1.25f, _distance / _tripLength);
            }
        }

        private void SpawnRow(float distance)
        {
            Biome biome = BiomeAt(distance);

            // На реке — проломы моста через всю дорогу, их только перепрыгнуть
            if (biome == Biome.River && Random.value < 0.5f)
            {
                AddObject(RoadObjectType.Gap, -1, distance);
                return;
            }

            // 1–2 препятствия, хотя бы одна полоса всегда свободна
            int count = Random.value < 0.35f ? 2 : 1;
            var lanes = new List<int> { 0, 1, 2 };
            for (int i = 0; i < count; i++)
            {
                int lane = lanes[Random.Range(0, lanes.Count)];
                lanes.Remove(lane);
                AddObject(PickObstacle(biome), lane, distance);
            }

            if (lanes.Count > 0 && Random.value < _horseshoeChance)
            {
                AddObject(RoadObjectType.Horseshoe, lanes[Random.Range(0, lanes.Count)], distance);
            }
        }

        private static RoadObjectType PickObstacle(Biome biome)
        {
            switch (biome)
            {
                case Biome.Forest: return Random.value < 0.6f ? RoadObjectType.Log : RoadObjectType.Tree;
                case Biome.Field: return Random.value < 0.7f ? RoadObjectType.Hole : RoadObjectType.Log;
                case Biome.Mountains: return Random.value < 0.7f ? RoadObjectType.Rock : RoadObjectType.Hole;
                default: return Random.value < 0.5f ? RoadObjectType.Log : RoadObjectType.Rock;
            }
        }

        private void SpawnEncounters()
        {
            for (int i = 0; i < _encounterPoints.Length && i < _encounters.Count; i++)
            {
                RoadObject traveler = AddObject(RoadObjectType.Traveler, 2, _encounterPoints[i] * _tripLength);
                traveler.option = i;
                traveler.view.position = new Vector3(_laneX[_laneX.Length - 1] + 1.8f, traveler.view.position.y, 0f);
            }
        }

        // Указатели развилки: полоса = город следующего ряда карты
        private void SpawnFork()
        {
            // Ехать некуда (конец карты) — один указатель «прибытие»
            if (_options.Count == 0)
            {
                RoadObject arrival = AddObject(RoadObjectType.Signpost, 1, _tripLength);
                var arrivalLabel = arrival.view.Find("Label")?.GetComponent<TextMeshPro>();
                if (arrivalLabel != null) arrivalLabel.text = "Прибытие";
                return;
            }

            for (int i = 0; i < _options.Count; i++)
            {
                RoadObject sign = AddObject(RoadObjectType.Signpost, OptionLane(i), _tripLength);
                sign.option = i;

                TownDefinition town = CampaignRun.TownOf(_options[i]);
                var label = sign.view.Find("Label")?.GetComponent<TextMeshPro>();
                if (label != null) label.text = town != null ? town.displayName : "?";
            }
        }

        // 1 город — любая полоса; 2 — левая и правая; 3 — по полосе на город
        private int OptionLane(int option)
        {
            if (_options.Count <= 1) return 1;
            if (_options.Count == 2) return option == 0 ? 0 : 2;
            return Mathf.Clamp(option, 0, _laneX.Length - 1);
        }

        private int OptionForLane(int lane)
        {
            if (_options.Count == 0) return -1;
            if (_options.Count == 1) return 0;
            if (_options.Count == 2)
            {
                if (lane == 0) return 0;
                if (lane == 2) return 1;
                RunMap map = CampaignRun.Map;
                int planned = map != null ? _options.FindIndex(o => o.index == map.plannedIndex) : -1;
                return planned >= 0 ? planned : 0;
            }
            return Mathf.Clamp(lane, 0, _options.Count - 1);
        }

        private RoadObject AddObject(RoadObjectType type, int lane, float distance)
        {
            var go = new GameObject(type.ToString());
            go.transform.SetParent(transform, false);
            go.AddComponent<VisualSlot>().Configure(VisualFor(type), 10, type == RoadObjectType.Signpost || type == RoadObjectType.Traveler);

            float x = lane >= 0 ? _laneX[lane] : 0f;
            go.transform.position = new Vector3(x, ScreenY(distance), 0f);

            var item = new RoadObject { type = type, lane = lane, distance = distance, view = go.transform };
            _objects.Add(item);
            return item;
        }

        private static VisualId VisualFor(RoadObjectType type)
        {
            switch (type)
            {
                case RoadObjectType.Log: return VisualId.RoadLog;
                case RoadObjectType.Hole: return VisualId.RoadHole;
                case RoadObjectType.Rock: return VisualId.RoadRock;
                case RoadObjectType.Tree: return VisualId.RoadTree;
                case RoadObjectType.Gap: return VisualId.RoadGap;
                case RoadObjectType.Horseshoe: return VisualId.Horseshoe;
                case RoadObjectType.Traveler: return VisualId.Traveler;
                default: return VisualId.Signpost;
            }
        }

        private float ScreenY(float distance) => _coachY + (distance - _distance);

        private static bool IsLow(RoadObjectType type) => type == RoadObjectType.Log || type == RoadObjectType.Hole || type == RoadObjectType.Gap;

        private void UpdateObjects()
        {
            for (int i = _objects.Count - 1; i >= 0; i--)
            {
                RoadObject item = _objects[i];
                if (item.view == null) { _objects.RemoveAt(i); continue; }

                Vector3 position = item.view.position;
                position.y = ScreenY(item.distance);
                item.view.position = position;

                if (!item.consumed && _distance >= item.distance) Reach(item);

                if (position.y < _coachY - 6f)
                {
                    Destroy(item.view.gameObject);
                    _objects.RemoveAt(i);
                }
            }
        }

        // Объект доехал до дилижанса
        private void Reach(RoadObject item)
        {
            item.consumed = true;

            switch (item.type)
            {
                case RoadObjectType.Horseshoe:
                    if (item.lane == _lane && !IsAirborne)
                    {
                        GameStats.Horseshoes++;
                        item.view.gameObject.SetActive(false);
                    }
                    break;

                case RoadObjectType.Traveler:
                    if (_encounterPanel != null && item.option >= 0 && item.option < _encounters.Count)
                    {
                        _encounterPanel.Show(_encounters[item.option], null);
                    }
                    break;

                case RoadObjectType.Signpost:
                    if (!_finished) ArriveAtFork();
                    break;

                default:
                    bool sameLane = item.lane < 0 || item.lane == _lane;
                    if (sameLane && !(IsLow(item.type) && IsAirborne)) Crash();
                    break;
            }
        }

        private void Crash()
        {
            _crashFlash = 0.35f;
            if (_wheels <= 0) return;

            _wheels--;
            if (_wheels == 0)
            {
                _brokenDown = true;
                GameStats.NextBattleWallDamage = Mathf.Max(GameStats.NextBattleWallDamage, _brokenCoachWallDamage);
            }
        }

        // ---------------------------------------------------------------- Развилка

        private void ArriveAtFork()
        {
            _finished = true;
            ChosenOption = OptionForLane(_lane);

            RunMap map = CampaignRun.Map;
            if (map != null && ChosenOption >= 0 && ChosenOption < _options.Count)
            {
                map.TravelTo(_options[ChosenOption].index);
                TownDefinition town = CampaignRun.TownOf(map.Current);
                if (town != null) GameStats.CurrentTownId = town.Id;
            }

            if (_hintText != null)
            {
                TownDefinition town = ChosenOption >= 0 && ChosenOption < _options.Count ? CampaignRun.TownOf(_options[ChosenOption]) : null;
                _hintText.text = town != null ? $"Дорога ведёт в {town.displayName}" : "Приехали";
            }

            Invoke(nameof(LoadArrival), 1.2f);
        }

        private void LoadArrival()
        {
            GamePause.Clear();
            SceneManager.LoadScene(_arrivalScene);
        }

        // Для проверок и отладки: перемотать дорогу (объекты до этой точки пропускаются)
        public void SkipTo(float distance)
        {
            distance = Mathf.Min(distance, _tripLength - 0.5f);
            foreach (RoadObject item in _objects)
            {
                if (item.distance <= distance && item.type != RoadObjectType.Signpost) item.consumed = true;
            }
            _distance = distance;
            _nextRowDistance = Mathf.Max(_nextRowDistance, distance + _viewAhead);
        }

        // ---------------------------------------------------------------- Вид

        private void BuildRoad()
        {
            _road = CreateRect("Road", new Vector2(8.4f, 40f), new Color(0.55f, 0.45f, 0.32f), -50);
            _leftSide = CreateRect("SideLeft", new Vector2(6f, 40f), Color.green, -51);
            _rightSide = CreateRect("SideRight", new Vector2(6f, 40f), Color.green, -51);
            _leftSide.transform.position = new Vector3(-7.2f, 0f, 0f);
            _rightSide.transform.position = new Vector3(7.2f, 0f, 0f);

            // Разметка между полосами — двигается, чтобы чувствовалась скорость
            for (int lane = 0; lane < _laneX.Length - 1; lane++)
            {
                float x = (_laneX[lane] + _laneX[lane + 1]) * 0.5f;
                for (int i = 0; i < 10; i++)
                {
                    SpriteRenderer dash = CreateRect($"Dash_{lane}_{i}", new Vector2(0.12f, 1.2f), new Color(1f, 1f, 1f, 0.35f), -49);
                    dash.transform.position = new Vector3(x, -12f + i * 3f, 0f);
                    _dashes.Add(dash.transform);
                }
            }

            var coachGo = new GameObject("Stagecoach");
            coachGo.transform.SetParent(transform, false);
            coachGo.transform.position = new Vector3(_laneX[_lane], _coachY, 0f);
            _coachVisual = coachGo.AddComponent<VisualSlot>();
            _coachVisual.Configure(VisualId.Stagecoach, 20);
            _coach = coachGo.transform;
            _coachBaseScale = _coach.localScale;
        }

        private SpriteRenderer CreateRect(string objectName, Vector2 size, Color color, int order)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(transform, false);
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = PlaceholderSprites.Get(PlaceholderShape.Rectangle);
            renderer.color = color;
            renderer.sortingOrder = order;
            return renderer;
        }

        private void UpdateCoach(float dt)
        {
            if (_coach == null) return;

            Vector3 position = _coach.position;
            float t = 1f - Mathf.Exp(-_laneChangeSharpness * dt);
            position.x = Mathf.Lerp(position.x, _laneX[_lane], t);
            _coach.position = position;

            // Прыжок: дилижанс «подлетает» — становится крупнее
            float jump = IsAirborne ? Mathf.Sin(Mathf.PI * (1f - _airTime / _jumpDuration)) : 0f;
            _coach.localScale = _coachBaseScale * (1f + (_jumpScale - 1f) * jump);

            if (_coachVisual != null && _coachVisual.Renderer != null)
            {
                _crashFlash = Mathf.Max(0f, _crashFlash - dt);
                _coachVisual.Renderer.color = _crashFlash > 0f ? new Color(1f, 0.3f, 0.3f) : (_brokenDown ? new Color(0.6f, 0.6f, 0.6f) : Color.white);
            }
        }

        private void UpdateScenery(float dt)
        {
            foreach (Transform dash in _dashes)
            {
                Vector3 position = dash.position;
                position.y -= Speed * dt;
                if (position.y < -15f) position.y += 30f;
                dash.position = position;
            }

            Color road, side;
            switch (CurrentBiome)
            {
                case Biome.Forest: road = new Color(0.5f, 0.38f, 0.25f); side = new Color(0.16f, 0.4f, 0.2f); break;
                case Biome.Field: road = new Color(0.62f, 0.5f, 0.32f); side = new Color(0.58f, 0.68f, 0.3f); break;
                case Biome.Mountains: road = new Color(0.5f, 0.5f, 0.52f); side = new Color(0.38f, 0.36f, 0.4f); break;
                default: road = new Color(0.55f, 0.42f, 0.28f); side = new Color(0.25f, 0.45f, 0.75f); break;
            }

            if (_road != null) _road.color = Color.Lerp(_road.color, road, dt * 2f);
            if (_leftSide != null) _leftSide.color = Color.Lerp(_leftSide.color, side, dt * 2f);
            if (_rightSide != null) _rightSide.color = Color.Lerp(_rightSide.color, side, dt * 2f);
        }

        private void UpdateHud()
        {
            if (_wheelsText != null) _wheelsText.text = _brokenDown ? "<color=#FF5050>Колёса: 0 — стена пострадает</color>" : $"Колёса: {_wheels} / {_maxWheels}";
            if (_horseshoesText != null) _horseshoesText.text = $"Подковы: {GameStats.Horseshoes}";
            if (_progress != null) _progress.value = Mathf.Clamp01(_distance / _tripLength);

            if (_biomeText != null)
            {
                _biomeText.text = CurrentBiome switch
                {
                    Biome.Forest => "Лес",
                    Biome.Field => "Поле",
                    Biome.Mountains => "Горы",
                    _ => "Река"
                };
            }

            if (_hintText != null && !_finished)
            {
                _hintText.text = _distance > _tripLength - 25f && _options.Count > 1
                    ? "Развилка! Выберите полосу — город"
                    : "Свайп — полоса, тап — прыжок";
            }
        }
    }
}
