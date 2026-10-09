using UnityEngine;

namespace Tanks2D
{
    // Полоска здоровья над врагом на двух SpriteRenderer (фон + заливка), без Canvas.
    // Сборщик проекта создаёт дочерние объекты "Background" и "Fill"; заливка уменьшается от правого края.
    public class EnemyHealthBar2D : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _background;
        [SerializeField] private SpriteRenderer _fill;
        [SerializeField] private Vector2 _size = new Vector2(0.9f, 0.12f);
        [SerializeField] private float _border = 0.03f;

        public void Build(Sprite sprite, Vector2 size, int sortingOrder)
        {
            _size = size;
            _background = CreatePart("Background", sprite, new Color(0.1f, 0.1f, 0.1f, 0.9f), sortingOrder);
            _fill = CreatePart("Fill", sprite, new Color(0.9f, 0.2f, 0.2f, 1f), sortingOrder + 1);
            _background.transform.localScale = new Vector3(_size.x, _size.y, 1f);
            SetNormalized(1f);
        }

        public Vector2 Size => _size;

        public void SetFillColor(Color color)
        {
            if (_fill != null) _fill.color = color;
        }

        public void SetNormalized(float value)
        {
            if (_fill == null) return;

            value = Mathf.Clamp01(value);
            float innerWidth = Mathf.Max(0f, _size.x - _border * 2f);
            float innerHeight = Mathf.Max(0f, _size.y - _border * 2f);
            float width = innerWidth * value;

            _fill.transform.localScale = new Vector3(width, innerHeight, 1f);
            _fill.transform.localPosition = new Vector3(-(innerWidth - width) * 0.5f, 0f, -0.001f);
            _fill.enabled = value > 0f;
        }

        private SpriteRenderer CreatePart(string partName, Sprite sprite, Color color, int sortingOrder)
        {
            Transform existing = transform.Find(partName);
            GameObject go = existing != null ? existing.gameObject : new GameObject(partName);
            go.transform.SetParent(transform, false);

            SpriteRenderer spriteRenderer = go.GetComponent<SpriteRenderer>();
            if (spriteRenderer == null) spriteRenderer = go.AddComponent<SpriteRenderer>();

            spriteRenderer.sprite = sprite;
            spriteRenderer.color = color;
            spriteRenderer.sortingOrder = sortingOrder;
            return spriteRenderer;
        }
    }
}
