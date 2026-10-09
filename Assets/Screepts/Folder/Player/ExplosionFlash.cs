using UnityEngine;

namespace Tanks2D
{
    // Вспышка взрыва: оранжевый круг расширяется и гаснет. Заглушка — меняется на свой эффект позже.
    public class ExplosionFlash : MonoBehaviour
    {
        private const float Duration = 0.25f;

        private SpriteRenderer _renderer;
        private float _radius;
        private float _time;

        public static void Spawn(Vector3 position, float radius)
        {
            var go = new GameObject("ExplosionFlash");
            go.transform.position = new Vector3(position.x, position.y, 0f);

            ExplosionFlash flash = go.AddComponent<ExplosionFlash>();
            flash._radius = radius;
            flash._renderer = go.AddComponent<SpriteRenderer>();
            flash._renderer.sprite = PlaceholderSprites.Get(PlaceholderShape.Circle);
            flash._renderer.sortingOrder = 45;
            flash.Apply(0f);
        }

        private void Update()
        {
            _time += Time.deltaTime;
            Apply(_time / Duration);
            if (_time >= Duration) Destroy(gameObject);
        }

        private void Apply(float t)
        {
            float size = _radius * 2f * Mathf.Lerp(0.4f, 1f, t);
            transform.localScale = new Vector3(size, size, 1f);
            _renderer.color = new Color(1f, 0.55f, 0.15f, Mathf.Lerp(0.6f, 0f, t));
        }
    }
}
