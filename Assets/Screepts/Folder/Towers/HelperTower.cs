using UnityEngine;

namespace Tanks2D
{
    // Башня-помощник: сама целится в ближайшего врага и стреляет теми же пулями, что и герой
    // (пули тоже отскакивают от бортов). Урон — доля от текущего урона героя.
    public class HelperTower : MonoBehaviour
    {
        [SerializeField] private Transform _turret;
        [SerializeField] private Transform _firePoint;
        [SerializeField] private GameObject _bulletPrefab;

        [Header("Стрельба")]
        [SerializeField] private float _fireDelay = 1f;
        [SerializeField] private float _bulletSpeed = 12f;
        [Tooltip("Урон пули = урон героя × множитель")]
        [SerializeField, Range(0.1f, 2f)] private float _damageMultiplier = 0.5f;
        [Tooltip("Дальность стрельбы в мировых единицах")]
        [SerializeField] private float _range = 20f;

        private float _nextFireTime;

        private void Start()
        {
            if (_turret == null) _turret = transform;
            // Разносим первые выстрелы, чтобы башни не стреляли синхронно
            _nextFireTime = Time.time + Random.Range(0f, _fireDelay);
        }

        private void Update()
        {
            if (GamePause.IsPaused || Wall.IsGameOver) return;

            PigEnemy target = PigEnemy.FindNearest(_turret.position);
            if (target == null) return;

            Vector2 direction = target.transform.position - _turret.position;
            if (direction.sqrMagnitude > _range * _range) return;

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            _turret.rotation = Quaternion.Euler(0f, 0f, angle - 90f);

            if (Time.time < _nextFireTime || _bulletPrefab == null) return;

            Vector3 position = _firePoint != null ? _firePoint.position : _turret.position;
            position.z = 0f;

            int damage = Mathf.Max(1, Mathf.RoundToInt(GameStats.BulletDamage * _damageMultiplier));
            PlayerController2D.SpawnBullet(_bulletPrefab, position, _turret.rotation, _bulletSpeed, damage);
            _nextFireTime = Time.time + _fireDelay;
        }
    }
}
