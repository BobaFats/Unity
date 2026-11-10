using System.Collections.Generic;
using UnityEngine;

namespace Tanks2D
{
    // Осколки при попадании и смерти: маленькие квадратики разлетаются, замедляются и гаснут.
    // Один объект на сцену двигает все осколки сразу; осколки переиспользуются (не больше MaxParticles).
    // Заглушка: позже можно заменить на свой эффект (Particle System) в GameFeel.Burst.
    public class HitParticles : MonoBehaviour
    {
        private const int MaxParticles = 300;
        private const float Lifetime = 0.45f;
        private const float Drag = 6f;
        private const int SortingOrder = 40;

        private class Particle
        {
            public Transform transform;
            public SpriteRenderer renderer;
            public Vector3 velocity;
            public float age;
            public float size;
            public Color color;
        }

        private static HitParticles _instance;
        private readonly List<Particle> _active = new List<Particle>();
        private readonly Stack<Particle> _free = new Stack<Particle>();
        private int _created;

        public static int ActiveCount => _instance != null ? _instance._active.Count : 0;

        public static void Emit(Vector3 position, Color color, int count, float speed)
        {
            if (_instance == null) _instance = new GameObject("HitParticles").AddComponent<HitParticles>();
            _instance.EmitInternal(position, color, count, speed);
        }

        private void EmitInternal(Vector3 position, Color color, int count, float speed)
        {
            color.a = 1f;
            for (int i = 0; i < count; i++)
            {
                Particle particle = Take();
                if (particle == null) return;

                Vector2 direction = Random.insideUnitCircle.normalized;
                particle.velocity = direction * speed * Random.Range(0.5f, 1.3f);
                particle.age = 0f;
                particle.size = Random.Range(0.08f, 0.18f);
                particle.color = color;
                particle.transform.position = new Vector3(position.x, position.y, 0f);
                particle.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
                particle.transform.localScale = Vector3.one * particle.size;
                particle.renderer.color = color;
                particle.transform.gameObject.SetActive(true);
                _active.Add(particle);
            }
        }

        private Particle Take()
        {
            if (_free.Count > 0) return _free.Pop();
            if (_created >= MaxParticles) return null;

            var go = new GameObject("Shard");
            go.transform.SetParent(transform, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = PlaceholderSprites.Get(PlaceholderShape.Rectangle);
            renderer.sortingOrder = SortingOrder;
            _created++;
            return new Particle { transform = go.transform, renderer = renderer };
        }

        private void Update()
        {
            if (GamePause.IsPaused) return;

            float dt = Time.deltaTime;
            float drag = Mathf.Exp(-Drag * dt);

            for (int i = _active.Count - 1; i >= 0; i--)
            {
                Particle particle = _active[i];
                particle.age += dt;

                if (particle.age >= Lifetime)
                {
                    particle.transform.gameObject.SetActive(false);
                    _active.RemoveAt(i);
                    _free.Push(particle);
                    continue;
                }

                particle.velocity *= drag;
                particle.transform.position += particle.velocity * dt;

                float t = particle.age / Lifetime;
                particle.transform.localScale = Vector3.one * (particle.size * (1f - t * 0.6f));
                Color color = particle.color;
                color.a = 1f - t * t;
                particle.renderer.color = color;
            }
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}
