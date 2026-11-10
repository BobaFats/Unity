using System.Collections.Generic;
using UnityEngine;

namespace Tanks2D
{
    // Физическая пуля: Rigidbody2D (Dynamic, без гравитации) с упругим материалом.
    // Летит строго по прямой, без самонаведения; от боковых стен поля (коллайдеры Bounds)
    // отскакивает по законам физики — угол падения равен углу отражения.
    // Врагов задевает через триггеры, поэтому от них не отскакивает, а наносит урон и исчезает.
    // Пули берутся из пула (ObjectPool): «уничтожение» возвращает пулю в пул.
    [RequireComponent(typeof(Rigidbody2D))]
    public class BulletMovement2D : MonoBehaviour, IPoolable
    {
        public const string LayerName = "Bullet";

        [SerializeField] private float _lifetime = 4f;
        [Tooltip("Сколько раз пуля может отскочить от стен")]
        [SerializeField] private int _maxBounces = 3;

        private Rigidbody2D _body;
        private float _speed;
        private int _damage;
        private int _bounces;
        private bool _hasHit;
        private int _pierceLeft;
        private float _age;
        private readonly HashSet<PigEnemy> _hitEnemies = new HashSet<PigEnemy>();

        public int PierceLeft => _pierceLeft;

        public int Bounces => _bounces;
        public Vector2 Velocity => _body != null ? _body.linearVelocity : Vector2.zero;

        // Пули не сталкиваются друг с другом
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ConfigureCollisions()
        {
            int layer = LayerMask.NameToLayer(LayerName);
            if (layer >= 0) Physics2D.IgnoreLayerCollision(layer, layer, true);
        }

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
        }

        public void Initialize(float speed, int damage)
        {
            _speed = speed;
            _damage = damage;
            _pierceLeft = GameStats.Pierce;
            _age = 0f;
            _body.position = transform.position;
            _body.rotation = transform.eulerAngles.z;
            _body.linearVelocity = transform.up * speed;
        }

        public void OnSpawnedFromPool()
        {
            _bounces = 0;
            _hasHit = false;
            _age = 0f;
            _hitEnemies.Clear();
            if (_body == null) _body = GetComponent<Rigidbody2D>();
            _body.linearVelocity = Vector2.zero;
            _body.angularVelocity = 0f;
            foreach (TrailRenderer trail in GetComponentsInChildren<TrailRenderer>(true)) trail.Clear();
        }

        public void OnReturnedToPool()
        {
        }

        private void Remove() => ObjectPool.Release(gameObject);

        private void FixedUpdate()
        {
            Vector2 velocity = _body.linearVelocity;
            if (velocity.sqrMagnitude < 0.0001f) return;

            // Упругий отскок без потери скорости; поворачиваем спрайт по направлению полёта
            _body.linearVelocity = velocity.normalized * _speed;
            _body.rotation = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg - 90f;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            if (_age >= _lifetime)
            {
                Remove();
                return;
            }

            PlayField field = PlayField.Instance;
            if (field == null) return;

            float y = transform.position.y;
            if (y > field.Top + 2f || y < field.Bottom - 2f) Remove();
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            _bounces++;
            if (_bounces > _maxBounces) Remove();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_hasHit) return;

            PigEnemy enemy = other.GetComponentInParent<PigEnemy>();
            if (enemy == null || enemy.IsDying || _hitEnemies.Contains(enemy)) return;

            _hitEnemies.Add(enemy);
            Vector3 hitPoint = enemy.transform.position;
            ElementSystem.ResolveHit(enemy, _damage, _body.linearVelocity.normalized);
            if (GameStats.ExplosionLevel > 0) Explode(hitPoint, enemy);

            // Пробивание: летит дальше, пока есть запас
            if (_pierceLeft > 0)
            {
                _pierceLeft--;
                return;
            }

            _hasHit = true;
            Remove();
        }

        // Взрывные пули: часть урона всем врагам в радиусе вокруг цели
        private void Explode(Vector3 center, PigEnemy direct)
        {
            float radius = GameStats.ExplosionRadius;
            int splash = Mathf.Max(1, Mathf.RoundToInt(_damage * GameStats.ExplosionDamageFraction));

            foreach (PigEnemy enemy in new List<PigEnemy>(PigEnemy.Alive))
            {
                if (enemy == direct || enemy.IsDying) continue;
                if (((Vector2)(enemy.transform.position - center)).sqrMagnitude <= radius * radius) enemy.ApplyDamage(splash, new Color(1f, 0.6f, 0.2f));
            }

            ExplosionFlash.Spawn(center, radius);
            GameFeel.Shake(GameFeel.ShakeExplosion);
        }
    }
}
