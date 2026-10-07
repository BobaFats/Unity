using System.Collections.Generic;
using UnityEngine;

namespace Tanks2D
{
    // Ставит башни-помощники в фиксированные слоты на стене (максимум 5).
    // Количество башен хранится в GameStats, поэтому в следующем бою они появляются снова.
    public class TowerManager : MonoBehaviour
    {
        [SerializeField] private GameObject _towerPrefab;
        [SerializeField] private Wall _wall;
        [Tooltip("Позиции слотов по X в долях полуширины стены (-1 — левый край, 1 — правый), в порядке постройки")]
        [SerializeField] private float[] _slots = { -0.4f, 0.4f, -0.8f, 0.8f, 0f };
        [Tooltip("Смещение башен по Y относительно центра стены")]
        [SerializeField] private float _offsetY = 0f;

        private readonly List<GameObject> _towers = new List<GameObject>();

        public int MaxTowers => Mathf.Min(GameStats.MaxTowers, _slots.Length);
        public bool CanAddTower => _towerPrefab != null && GameStats.TowerCount < MaxTowers;

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

        private void SpawnTower(int slotIndex)
        {
            Vector3 center = _wall != null ? _wall.transform.position : transform.position;
            float halfWidth = _wall != null ? _wall.HalfWidth : 4.5f;

            var position = new Vector3(center.x + _slots[slotIndex] * halfWidth, center.y + _offsetY, 0f);
            GameObject tower = Instantiate(_towerPrefab, position, Quaternion.identity, transform);
            tower.name = $"Tower_{slotIndex + 1}";
            _towers.Add(tower);
        }
    }
}
