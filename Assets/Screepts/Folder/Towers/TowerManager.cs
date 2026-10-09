using System.Collections.Generic;
using UnityEngine;

namespace Tanks2D
{
    // Ставит башни-помощники в слоты (максимум 5). Слоты — дочерние объекты Slot_1..Slot_5:
    // двигайте их мышкой в сцене, башни появятся ровно там. Новая башня встаёт в первый свободный слот.
    // Количество башен хранится в GameStats; разрушенная боссом башня теряется и в следующих боях.
    public class TowerManager : MonoBehaviour
    {
        [SerializeField] private GameObject _towerPrefab;
        [SerializeField] private Wall _wall;

        [Tooltip("Точки появления башен по порядку постройки. Двигайте их в сцене")]
        [SerializeField] private Transform[] _slotPoints = new Transform[0];

        [Header("Запасной вариант, если точки не заданы")]
        [Tooltip("Позиции слотов по X в долях полуширины стены (-1 — левый край, 1 — правый)")]
        [SerializeField] private float[] _slots = { -0.4f, 0.4f, -0.8f, 0.8f, 0f };
        [SerializeField] private float _offsetY = 0f;

        private HelperTower[] _occupied = new HelperTower[0];

        public static TowerManager Instance { get; private set; }

        private bool UsePoints => _slotPoints != null && _slotPoints.Length > 0;
        private int SlotCount => UsePoints ? _slotPoints.Length : _slots.Length;

        public int MaxTowers => Mathf.Min(GameStats.MaxTowers, SlotCount);
        public bool CanAddTower => _towerPrefab != null && GameStats.TowerCount < MaxTowers && FirstFreeSlot() >= 0;

        // Живые башни по порядку слотов
        public IReadOnlyList<GameObject> Towers
        {
            get
            {
                var list = new List<GameObject>();
                foreach (HelperTower tower in _occupied)
                {
                    if (tower != null) list.Add(tower.gameObject);
                }
                return list;
            }
        }

        // OnEnable, а не Awake: переживает перезагрузку скриптов прямо в Play Mode
        private void OnEnable()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            _occupied = new HelperTower[SlotCount];
            int count = Mathf.Min(GameStats.TowerCount, MaxTowers);
            for (int i = 0; i < count; i++) SpawnTower(i);
        }

        public void AddTower()
        {
            if (!CanAddTower) return;

            SpawnTower(FirstFreeSlot());
            GameStats.TowerCount++;
        }

        // Случайная башня, которую ещё не собираются разрушить
        public HelperTower GetRandomTower()
        {
            var candidates = new List<HelperTower>();
            foreach (HelperTower tower in _occupied)
            {
                if (tower != null && !tower.IsDoomed) candidates.Add(tower);
            }
            return candidates.Count > 0 ? candidates[Random.Range(0, candidates.Count)] : null;
        }

        public void DestroyTower(HelperTower tower)
        {
            for (int i = 0; i < _occupied.Length; i++)
            {
                if (_occupied[i] != tower) continue;

                _occupied[i] = null;
                GameStats.TowerCount = Mathf.Max(0, GameStats.TowerCount - 1);
                tower.Demolish();
                return;
            }
        }

        public Vector3 GetSlotPosition(int index)
        {
            if (UsePoints && index < _slotPoints.Length && _slotPoints[index] != null)
            {
                Vector3 point = _slotPoints[index].position;
                return new Vector3(point.x, point.y, 0f);
            }

            Vector3 center = _wall != null ? _wall.transform.position : transform.position;
            float halfWidth = _wall != null ? _wall.HalfWidth : 4.5f;
            float slot = _slots.Length > 0 ? _slots[index % _slots.Length] : 0f;
            return new Vector3(center.x + slot * halfWidth, center.y + _offsetY, 0f);
        }

        private int FirstFreeSlot()
        {
            if (_occupied.Length != SlotCount) System.Array.Resize(ref _occupied, SlotCount);
            for (int i = 0; i < _occupied.Length; i++)
            {
                if (_occupied[i] == null) return i;
            }
            return -1;
        }

        private void SpawnTower(int slotIndex)
        {
            if (slotIndex < 0) return;

            GameObject go = Instantiate(_towerPrefab, GetSlotPosition(slotIndex), Quaternion.identity, transform);
            go.name = $"Tower_{slotIndex + 1}";

            HelperTower tower = go.GetComponent<HelperTower>();
            if (tower == null) tower = go.AddComponent<HelperTower>();
            _occupied[slotIndex] = tower;
        }

        private void OnDrawGizmos()
        {
            if (!UsePoints) return;

            Gizmos.color = new Color(0.3f, 0.9f, 0.8f);
            for (int i = 0; i < _slotPoints.Length; i++)
            {
                if (_slotPoints[i] == null) continue;
                Gizmos.DrawWireCube(_slotPoints[i].position, new Vector3(0.9f, 0.9f, 0f));
#if UNITY_EDITOR
                UnityEditor.Handles.Label(_slotPoints[i].position + new Vector3(-0.3f, 0.75f, 0f), $"Башня {i + 1}");
#endif
            }
        }
    }
}
