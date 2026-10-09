using UnityEngine;
using UnityEngine.InputSystem;

namespace Tanks2D
{
    // Герой стоит внизу экрана, пушка поворачивается в верхнюю полуплоскость.
    // Стрельба автоматическая. Целится игрок:
    // ПК — мышью, телефон — пальцем по полю. Перезарядка автоматическая, вручную — R или кнопка в HUD.
    // Оружие (Turret) плавно поворачивается к прицелу и плавно следует за точкой крепления —
    // например, за костью руки модели героя (ищется автоматически по имени RightHand).
    // Вид оружия задаёт выбранный в лобби скин (WeaponSkinCatalog).
    public class PlayerController2D : MonoBehaviour
    {
        [Header("References (Ссылки)")]
        [Tooltip("Поворотная часть (ствол). Её ось Y смотрит в направлении выстрела.")]
        [SerializeField] private Transform _turret;
        [SerializeField] private Transform _firePoint;
        [SerializeField] private GameObject _bulletPrefab;
        [Tooltip("Анимации героя: Idle, Attack (выстрел), Special (перезарядка). Пусто — берутся с этого же объекта")]
        [SerializeField] private CharacterAnimations _animations;

        [Header("Weapon Settings (Настройки оружия)")]
        [SerializeField] private float _bulletSpeed = 14f;
        [Tooltip("Минимальный угол ствола над горизонтом, в градусах")]
        [SerializeField, Range(0f, 80f)] private float _minAimAngle = 10f;
        [Tooltip("Скорость поворота оружия к прицелу, градусов в секунду (0 — мгновенно)")]
        [SerializeField, Min(0f)] private float _aimSpeed = 720f;

        [Header("Оружие следует за героем")]
        [Tooltip("Внешний вид оружия (для скинов). Пусто — объект Weapon внутри Turret")]
        [SerializeField] private VisualSlot _weaponVisual;
        [Tooltip("Точка крепления оружия (например, кость руки). Пусто — ищется кость с именем ниже")]
        [SerializeField] private Transform _weaponMount;
        [SerializeField] private bool _autoFindHandBone = true;
        [SerializeField] private string _handBoneName = "RightHand";
        [Tooltip("Насколько плотно оружие следует за точкой крепления (больше — жёстче, меньше — плавнее)")]
        [SerializeField, Min(0f)] private float _followSharpness = 25f;

        private Camera _mainCamera;
        private float _nextFireTime;
        private int _currentAmmo;
        private float _reloadEndTime;
        private bool _isReloading;
        private bool _pressStartedOverUI;
        private Quaternion _targetRotation = Quaternion.identity;
        private Vector3 _mountOffset;
        private bool _hasMount;

        // Исходный вид оружия из сцены — для скина «по умолчанию»
        private bool _weaponDefaultsSaved;
        private Sprite _defaultSprite;
        private GameObject _defaultPrefab;
        private Color _defaultColor;
        private Vector3 _defaultOffset;
        private Vector3 _defaultRotation;
        private float _defaultScale;
        private Vector3 _defaultFirePoint;

        public int CurrentAmmo => _currentAmmo;
        public bool IsReloading => _isReloading;
        public float ReloadEndTime => _reloadEndTime;
        public Transform Turret => _turret;
        public Transform WeaponMount => _hasMount ? _weaponMount : null;

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
            if (_animations == null) _animations = GetComponent<CharacterAnimations>();
            _targetRotation = _turret.rotation;

            if (_weaponVisual == null && _turret != transform)
            {
                Transform weapon = _turret.Find("Weapon");
                if (weapon != null) _weaponVisual = weapon.GetComponent<VisualSlot>();
            }

            ApplyWeaponSkin();
            ResolveMount();
        }

        // ---------------------------------------------------------------- Скин оружия

        public void ApplyWeaponSkin()
        {
            if (_weaponVisual == null) return;

            if (!_weaponDefaultsSaved)
            {
                _weaponDefaultsSaved = true;
                _defaultSprite = _weaponVisual.CustomSprite;
                _defaultPrefab = _weaponVisual.CustomPrefab;
                _defaultColor = _weaponVisual.CustomColor;
                _defaultOffset = _weaponVisual.Offset;
                _defaultRotation = _weaponVisual.Rotation;
                _defaultScale = _weaponVisual.Scale;
                if (_firePoint != null) _defaultFirePoint = _firePoint.localPosition;
            }

            WeaponSkin skin = WeaponSkinCatalog.Selected;
            bool useDefault = skin == null || skin.IsDefault;

            if (useDefault || (skin.sprite == null && skin.prefab == null)) _weaponVisual.SetArt(_defaultSprite, _defaultPrefab, useDefault ? _defaultColor : skin.tint);
            else _weaponVisual.SetArt(skin.sprite, skin.prefab, skin.tint);

            if (!useDefault && skin.overridePlacement) _weaponVisual.SetPlacement(skin.offset, skin.rotation, skin.scale);
            else _weaponVisual.SetPlacement(_defaultOffset, _defaultRotation, _defaultScale);

            if (_firePoint != null) _firePoint.localPosition = !useDefault && skin.overrideFirePoint ? skin.firePointLocalPosition : _defaultFirePoint;
        }

        // ---------------------------------------------------------------- Крепление оружия к герою

        private void ResolveMount()
        {
            if (_weaponMount == null && _autoFindHandBone && !string.IsNullOrEmpty(_handBoneName))
            {
                _weaponMount = FindBone(transform, _handBoneName);
            }

            _hasMount = _weaponMount != null && _turret != transform && !_weaponMount.IsChildOf(_turret);

            // Сохраняем текущее положение оружия относительно точки крепления — как его расставили в сцене
            if (_hasMount) _mountOffset = _turret.position - _weaponMount.position;
        }

        private Transform FindBone(Transform root, string boneName)
        {
            foreach (Transform child in root)
            {
                if (child == _turret) continue;
                if (child.name == boneName) return child;

                Transform found = FindBone(child, boneName);
                if (found != null) return found;
            }
            return null;
        }

        // Точку крепления можно сменить из кода (например, при смене модели героя)
        public void SetWeaponMount(Transform mount, bool keepCurrentOffset = true)
        {
            _weaponMount = mount;
            _hasMount = mount != null && _turret != transform && !mount.IsChildOf(_turret);
            if (_hasMount) _mountOffset = keepCurrentOffset ? _turret.position - mount.position : Vector3.zero;
        }

        // После анимаций модели: оружие плавно догоняет руку и плавно поворачивается к прицелу
        private void LateUpdate()
        {
            if (_turret == null || _turret == transform) return;

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            _turret.rotation = _aimSpeed > 0f
                ? Quaternion.RotateTowards(_turret.rotation, _targetRotation, _aimSpeed * dt)
                : _targetRotation;

            if (_hasMount && _weaponMount != null)
            {
                Vector3 target = _weaponMount.position + _mountOffset;
                target.z = _turret.position.z;
                float t = _followSharpness > 0f ? 1f - Mathf.Exp(-_followSharpness * dt) : 1f;
                _turret.position = Vector3.Lerp(_turret.position, target, t);
            }
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

            _targetRotation = Quaternion.Euler(0f, 0f, angle - 90f);
            if (_turret == transform) _turret.rotation = _targetRotation;
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
            if (_animations != null) _animations.Play(CharacterAnimation.Special);
        }

        // Залп: GameStats.BulletCount пуль веером шириной GameStats.SpreadAngle (тратит один патрон)
        private void Shoot()
        {
            if (_bulletPrefab == null || _firePoint == null) return;
            if (_animations != null) _animations.Play(CharacterAnimation.Attack);

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
