using UnityEngine;
using UnityEngine.InputSystem;

namespace Tanks2D
{
    // Герой стоит внизу экрана, пушка поворачивается в верхнюю полуплоскость.
    // Стрельба автоматическая. Целится игрок:
    // ПК — мышью, телефон — пальцем по полю. Перезарядка автоматическая, вручную — R или кнопка в HUD.
    public class PlayerController2D : MonoBehaviour
    {
        [Header("References (Ссылки)")]
        [Tooltip("Поворотная часть (ствол). Её ось Y смотрит в направлении выстрела.")]
        [SerializeField] private Transform _turret;
        [SerializeField] private Transform _firePoint;
        [SerializeField] private GameObject _bulletPrefab;

        [Header("Weapon Settings (Настройки оружия)")]
        [SerializeField] private float _bulletSpeed = 14f;
        [Tooltip("Минимальный угол ствола над горизонтом, в градусах")]
        [SerializeField, Range(0f, 80f)] private float _minAimAngle = 10f;

        private Camera _mainCamera;
        private float _nextFireTime;
        private int _currentAmmo;
        private float _reloadEndTime;
        private bool _isReloading;
        private bool _pressStartedOverUI;

        public int CurrentAmmo => _currentAmmo;
        public bool IsReloading => _isReloading;
        public float ReloadEndTime => _reloadEndTime;

        public float ReloadProgress
        {
            get
            {
                if (!_isReloading) return 1f;
                float total = Mathf.Max(0.01f, GameStats.ReloadTime);
                return Mathf.Clamp01(1f - (_reloadEndTime - Time.time) / total);
            }
        }

        private void Start()
        {
            _mainCamera = Camera.main;
            _currentAmmo = GameStats.MaxAmmo;
            if (_turret == null) _turret = transform;
        }

        private void Update()
        {
            if (GamePause.IsPaused || Wall.IsGameOver) return;

            if (_isReloading && Time.time >= _reloadEndTime)
            {
                _currentAmmo = GameStats.MaxAmmo;
                _isReloading = false;
            }

            HandlePointer();
            TryShoot();

            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            {
                RequestReload();
            }
        }

        private void HandlePointer()
        {
            Pointer pointer = Pointer.current;
            if (pointer == null) return;

            Vector2 screenPosition = pointer.position.ReadValue();

            if (pointer.press.wasPressedThisFrame)
            {
                _pressStartedOverUI = PointerInput.IsOverUI(screenPosition);
            }

            bool isTouch = pointer is Touchscreen;
            bool isTouchingField = pointer.press.isPressed && !_pressStartedOverUI;

            // Целится игрок: мышь — всегда, палец — пока касается поля (иначе ствол держит последнее направление).
            // Стреляет герой сам: автоатака без нажатий.
            if (!isTouch || isTouchingField) AimAt(screenPosition);
        }

        // Вызывается кнопкой "Перезарядка" в HUD
        public void RequestReload()
        {
            if (_isReloading || _currentAmmo >= GameStats.MaxAmmo) return;
            StartReload();
        }

        private void AimAt(Vector2 screenPosition)
        {
            if (_mainCamera == null) return;

            Vector3 worldPoint = _mainCamera.ScreenToWorldPoint(screenPosition);
            Vector2 direction = worldPoint - _turret.position;
            if (direction.sqrMagnitude < 0.0001f) return;

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            // Ниже горизонта не целимся: прижимаем к ближайшему краю допустимого сектора
            if (angle < 0f) angle = direction.x >= 0f ? _minAimAngle : 180f - _minAimAngle;
            angle = Mathf.Clamp(angle, _minAimAngle, 180f - _minAimAngle);

            _turret.rotation = Quaternion.Euler(0f, 0f, angle - 90f);
        }

        private void TryShoot()
        {
            if (_isReloading || Time.time < _nextFireTime) return;
            if (_currentAmmo <= 0)
            {
                StartReload();
                return;
            }

            Shoot();
            _currentAmmo--;
            _nextFireTime = Time.time + GameStats.FireDelay;

            if (_currentAmmo <= 0) StartReload();
        }

        private void StartReload()
        {
            _isReloading = true;
            _reloadEndTime = Time.time + GameStats.ReloadTime;
        }

        // Залп: GameStats.BulletCount пуль веером шириной GameStats.SpreadAngle (тратит один патрон)
        private void Shoot()
        {
            if (_bulletPrefab == null || _firePoint == null) return;

            Vector3 position = _firePoint.position;
            position.z = 0f;

            int count = Mathf.Max(1, GameStats.BulletCount);
            float spread = count > 1 ? GameStats.SpreadAngle : 0f;

            for (int i = 0; i < count; i++)
            {
                float offset = count > 1 ? Mathf.Lerp(-spread * 0.5f, spread * 0.5f, i / (float)(count - 1)) : 0f;
                Quaternion rotation = _turret.rotation * Quaternion.Euler(0f, 0f, offset);
                SpawnBullet(_bulletPrefab, position, rotation, _bulletSpeed, GameStats.BulletDamage);
            }
        }

        public static void SpawnBullet(GameObject prefab, Vector3 position, Quaternion rotation, float speed, int damage)
        {
            GameObject bullet = Instantiate(prefab, position, rotation);

            BulletMovement2D bulletScript = bullet.GetComponent<BulletMovement2D>();
            if (bulletScript == null) bulletScript = bullet.AddComponent<BulletMovement2D>();
            bulletScript.Initialize(speed, damage);
        }
    }
}
