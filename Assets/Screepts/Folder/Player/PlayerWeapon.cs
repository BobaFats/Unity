using UnityEngine;
using UnityEngine.InputSystem; // Подключаем систему ввода для отслеживания мыши

namespace Tanks2D
{
    public class StationaryTurret : MonoBehaviour
    {
        [Header("References (Ссылки)")]
        [SerializeField] 
        [Tooltip("Точка на конце дула, откуда вылетает снаряд")]
        private Transform _firePoint;
        
        [SerializeField] 
        [Tooltip("Префаб снаряда (пули)")]
        private GameObject _bulletPrefab;

        [Header("Weapon Settings (Настройки оружия)")]
        [SerializeField] 
        [Tooltip("Скорость полета пули")]
        private float _bulletSpeed = 20f;
        
        [SerializeField] 
        [Tooltip("Задержка между выстрелами пушки в секундах (скорострельность)")]
        private float _fireRate = 0.2f; 

        private float _nextFireTime = 0f;
        private Camera _mainCamera;

        private void Start()
        {
            // Находим главную камеру на сцене для просчета координат мыши
            _mainCamera = Camera.main;
        }

        private void Update()
        {
            RotateTowardsMouse();
            
            // Стрельба по зажатию или клику левой кнопки мыши
            if (Mouse.current != null && Mouse.current.leftButton.isPressed)
            {
                if (Time.time >= _nextFireTime)
                {
                    Shoot();
                    _nextFireTime = Time.time + _fireRate;
                }
            }
        }

        private void RotateTowardsMouse()
        {
            if (_mainCamera == null) return;

            // 1. Получаем позицию курсора мыши на экране
            Vector2 mouseScreenPos = Mouse.current.position.ReadValue();

            // 2. Переводим экранные пиксели в мировые 2D координаты пространства игры
            Vector3 mouseWorldPos = _mainCamera.ScreenToWorldPoint(new Vector3(mouseScreenPos.x, mouseScreenPos.y, _mainCamera.nearClipPlane));
            mouseWorldPos.z = 0f; // Зануляем Z координату для двумерной плоскости

            // 3. Считаем вектор направления от центра пушки к курсору мыши
            Vector2 direction = (mouseWorldPos - transform.position).normalized;

            // 4. Вычисляем угол поворота в градусах по тригонометрической формуле Арктангенса
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            
            // 5. ЖЕСТКОЕ ОГРАНИЧЕНИЕ: Пушка вертится только в диапазоне 180 градусов (вправо от -90 до +90)
            angle = Mathf.Clamp(angle, -90f, 90f);

            // 6. Применяем вращение к пушке вокруг оси Z
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        private void Shoot()
        {
            if (_bulletPrefab == null || _firePoint == null) return;

            // Спавним пулю
            GameObject bullet = Instantiate(_bulletPrefab, _firePoint.position, _firePoint.rotation);
            
            // Насильно сбрасываем координату Z в 0, чтобы 3D-модель не улетала вглубь экрана
            Vector3 safePos = bullet.transform.position;
            safePos.z = 0f;
            bullet.transform.position = safePos;

            // ИСПРАВЛЕНО: Находим на пуле наш НОВЫЙ чистый скрипт движения и задаем скорость
            BulletMovement2D bulletScript = bullet.GetComponent<BulletMovement2D>();
            if (bulletScript == null)
            {
                bulletScript = bullet.AddComponent<BulletMovement2D>();
            }
            
            bulletScript.Initialize(_bulletSpeed, GameStats.BulletDamage);
        }
    }
}
