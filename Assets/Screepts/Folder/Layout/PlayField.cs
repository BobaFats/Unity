using UnityEngine;

namespace Tanks2D
{
    // Вертикальное игровое поле фиксированного размера в мировых единицах (по умолчанию 9 x 16).
    // Камера подгоняется так, чтобы поле всегда было видно целиком — на телефоне и на ПК.
    // Все игровые объекты расставляются относительно поля, а не экрана.
    public class PlayField : MonoBehaviour
    {
        [SerializeField] private Vector2 _size = new Vector2(9f, 16f);

        public static PlayField Instance { get; private set; }

        public Vector2 Size => _size;
        public Vector2 Center => transform.position;
        public float Left => Center.x - _size.x * 0.5f;
        public float Right => Center.x + _size.x * 0.5f;
        public float Bottom => Center.y - _size.y * 0.5f;
        public float Top => Center.y + _size.y * 0.5f;

        // OnEnable, а не Awake: переживает перезагрузку скриптов прямо в Play Mode
        private void OnEnable()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(transform.position, new Vector3(_size.x, _size.y, 0f));
        }
    }
}
