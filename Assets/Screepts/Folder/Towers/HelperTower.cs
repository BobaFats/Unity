using UnityEngine;

namespace Tanks2D
{
    // Башня-помощник: сама целится в ближайшего врага и стреляет теми же пулями, что и герой
    // (пули тоже отскакивают от стен). Урон — доля от текущего урона героя.
    // Босс со способностью DestroyTowers может её разрушить: башня мигает красным и рушится.
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

        [Tooltip("Анимации башни: Idle, Attack (выстрел), Death (разрушена). Пусто — берутся с этого же объекта")]
        [SerializeField] private CharacterAnimations _animations;

        [Header("Разрушение")]
        [SerializeField] private Color _warningColor = new Color(1f, 0.25f, 0.25f);
        [SerializeField] private float _warningBlinkRate = 8f;

        private float _nextFireTime;
        private bool _doomed;
        private float _warningTime;
        private VisualSlot _visual;
        private Color _baseColor = Color.white;

        public bool IsDoomed => _doomed;

        private void Start()
        {
            if (_turret == null) _turret = transform;
            if (_animations == null) _animations = GetComponent<CharacterAnimations>();
            _visual = GetComponent<VisualSlot>();
            if (_visual != null && _visual.Renderer != null) _baseColor = _visual.Renderer.color;

            // Разносим первые выстрелы, чтобы башни не стреляли синхронно
            _nextFireTime = Time.time + Random.Range(0f, _fireDelay);
        }

        private void Update()
        {
            if (GamePause.IsPaused || Wall.IsGameOver) return;

            if (_doomed)
            {
                Blink();
                return;
            }

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
            if (_animations != null) _animations.Play(CharacterAnimation.Attack);
            _nextFireTime = Time.time + _fireDelay;
        }

        // Босс выбрал эту башню: перестаёт стрелять и мигает
        public void MarkForDestruction(float warningSeconds)
        {
            _doomed = true;
            _warningTime = 0f;
        }

        public void CancelDestruction()
        {
            _doomed = false;
            SetColor(_baseColor);
        }

        // Разрушение: анимация смерти (если есть), затем объект удаляется
        public void Demolish()
        {
            _doomed = true;
            SetColor(_warningColor);

            float duration = _animations != null ? _animations.Play(CharacterAnimation.Death) : 0f;
            Destroy(gameObject, duration > 0f ? duration + 0.2f : 0.05f);
        }

        private void Blink()
        {
            _warningTime += Time.deltaTime;
            bool on = Mathf.Repeat(_warningTime * _warningBlinkRate, 2f) < 1f;
            SetColor(on ? _warningColor : _baseColor);
        }

        private void SetColor(Color color)
        {
            if (_visual != null && _visual.Renderer != null) _visual.Renderer.color = color;
        }
    }
}
