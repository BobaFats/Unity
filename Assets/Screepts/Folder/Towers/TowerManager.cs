using System.Collections.Generic;
using UnityEngine;

namespace Tanks2D
{
    // Ставит башни-помощники в слоты (максимум 5). Слоты — дочерние объекты Slot_1..Slot_5:
    // двигайте их мышкой в сцене, башни появятся ровно там. Порядок в списке = порядок постройки.
    // Количество башен хранится в GameStats, поэтому в следующем бою они появляются снова.
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

        private readonly List<GameObject> _towers = new List<GameObject>();

        private bool UsePoints => _slotPoints != null && _slotPoints.Length > 0;
        private int SlotCount => UsePoints ? _slotPoints.Length : _slots.Length;

        public int MaxTowers => Mathf.Min(GameStats.MaxTowers, SlotCount);
        public bool CanAddTower => _towerPrefab != null && GameStats.TowerCount < MaxTowers;
        public IReadOnlyList<GameObject> Towers => _towers;

        private void Start()
        {
            int count = Mathf.Min(GameStats.TowerCount, MaxTowers);
            for (int i = 0; i < count; i++) SpawnTower(i);
        }

        public void AddTower()
        {
            if (!CanAddTower) return;

            SpawnTower(GameStats.TowerCount);
            GameStats.TowerCount++;
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

        private void SpawnTower(int slotIndex)
        {
            GameObject tower = Instantiate(_towerPrefab, GetSlotPosition(slotIndex), Quaternion.identity, transform);
            tower.name = $"Tower_{slotIndex + 1}";
            _towers.Add(tower);
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
