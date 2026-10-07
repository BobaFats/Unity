using UnityEngine;

namespace Tanks2D
{
    // Пуля летит по своей оси Y. От боковых краёв поля отскакивает и после отскока
    // доворачивает на ближайшего врага. Нужен Rigidbody2D (Kinematic) и Collider2D с isTrigger.
    public class BulletMovement2D : MonoBehaviour
    {
        [SerializeField] private float _lifetime = 4f;
        [Tooltip("Сколько раз пуля может отскочить от бортов")]
        [SerializeField] private int _maxBounces = 3;
        [Tooltip("После отскока лететь в ближайшего врага (иначе — зеркальное отражение)")]
        [SerializeField] private bool _aimAtEnemyAfterBounce = true;
        [Tooltip("Радиус пули для расчёта касания борта")]
        [SerializeField] private float _radius = 0.11f;

        private float _speed;
        private int _damage;
        private int _bounces;
        private bool _hasHit;

        public void Initialize(float speed, int damage)
        {
            _speed = speed;
            _damage = damage;
            Destroy(gameObject, _lifetime);
        }

        private void Update()
        {
            transform.Translate(Vector3.up * _speed * Time.deltaTime, Space.Self);

            PlayField field = PlayField.Instance;
            if (field == null) return;

            Vector3 position = transform.position;
            Vector2 direction = transform.up;

            bool hitLeft = position.x < field.Left + _radius && direction.x < 0f;
            bool hitRight = position.x > field.Right - _radius && direction.x > 0f;

            if (hitLeft || hitRight)
            {
                Bounce(field, position, direction, hitLeft);
                return;
            }

            if (position.y > field.Top + 1f || position.y < field.Bottom - 1f)
            {
                Destroy(gameObject);
            }
        }

        private void Bounce(PlayField field, Vector3 position, Vector2 direction, bool fromLeft)
        {
            _bounces++;
            if (_bounces > _maxBounces)
            {
                Destroy(gameObject);
                return;
            }

            position.x = fromLeft ? field.Left + _radius : field.Right - _radius;
            transform.position = position;

            direction.x = -direction.x;

            if (_aimAtEnemyAfterBounce)
            {
                PigEnemy target = PigEnemy.FindNearest(position);
                if (target != null) direction = ((Vector2)target.transform.position - (Vector2)position).normalized;
            }

            SetDirection(direction);
        }

        private void SetDirection(Vector2 direction)
        {
            if (direction.sqrMagnitude < 0.0001f) return;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle - 90f);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_hasHit) return;

            PigEnemy enemy = other.GetComponentInParent<PigEnemy>();
            if (enemy == null || enemy.IsDying) return;

            _hasHit = true;
            enemy.ApplyDamage(_damage);
            Destroy(gameObject);
        }
    }
}
