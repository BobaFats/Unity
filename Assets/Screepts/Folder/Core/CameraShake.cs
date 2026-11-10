using UnityEngine;

namespace Tanks2D
{
    // Тряска камеры. Добавляется на камеру сама при первом GameFeel.Shake.
    // Каждый кадр сначала возвращает камеру на место, потом сдвигает её — поэтому не мешает
    // другим скриптам, которые ставят камеру (CameraFitter).
    [DefaultExecutionOrder(10000)]
    public class CameraShake : MonoBehaviour
    {
        private const float MaxStrength = 0.6f;

        private float _strength;
        private float _duration;
        private float _time;
        private Vector3 _applied;
        private Vector3 _shakenPosition;

        public bool IsShaking => _time < _duration;
        public Vector3 CurrentOffset => _applied;

        public void Add(float strength, float duration)
        {
            // Сильный удар перекрывает слабую тряску, слабый — не обрывает сильную
            float remaining = IsShaking ? _strength * (1f - _time / _duration) : 0f;
            _strength = Mathf.Min(MaxStrength, Mathf.Max(remaining, strength));
            _duration = Mathf.Max(0.01f, duration);
            _time = 0f;
        }

        private void LateUpdate()
        {
            // Убираем прошлый сдвиг, если камеру за кадр никто не поставил заново
            if (transform.position == _shakenPosition) transform.position -= _applied;
            _applied = Vector3.zero;

            if (!IsShaking || GamePause.IsPaused) return;

            _time += Time.deltaTime;
            float fade = 1f - Mathf.Clamp01(_time / _duration);
            Vector2 offset = Random.insideUnitCircle * (_strength * fade * fade);
            _applied = new Vector3(offset.x, offset.y, 0f);
            transform.position += _applied;
            _shakenPosition = transform.position;
        }

        private void OnDisable()
        {
            if (transform.position == _shakenPosition) transform.position -= _applied;
            _applied = Vector3.zero;
        }
    }
}
