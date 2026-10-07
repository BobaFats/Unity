using UnityEngine;

namespace Tanks2D
{
    // Подбирает orthographicSize так, чтобы PlayField целиком помещался в кадр при любом соотношении сторон:
    // узкий телефон — поле по ширине, широкий монитор ПК — поле по высоте (по бокам фон камеры).
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    public class CameraFitter : MonoBehaviour
    {
        [SerializeField] private PlayField _field;
        [SerializeField] private Vector2 _fallbackSize = new Vector2(9f, 16f);
        [Tooltip("Дополнительный отступ вокруг поля в мировых единицах")]
        [SerializeField] private float _padding = 0f;

        private Camera _camera;

        private void LateUpdate()
        {
            Fit();
        }

        public void Fit()
        {
            if (_camera == null) _camera = GetComponent<Camera>();
            if (_camera == null || !_camera.orthographic) return;

            Vector2 size = (_field != null ? _field.Size : _fallbackSize) + Vector2.one * (_padding * 2f);
            float aspect = Mathf.Max(0.01f, _camera.aspect);

            _camera.orthographicSize = Mathf.Max(size.y * 0.5f, size.x * 0.5f / aspect);

            if (_field != null)
            {
                Vector3 center = _field.transform.position;
                transform.position = new Vector3(center.x, center.y, transform.position.z);
            }
        }

        // Видимый прямоугольник мира для ортографической камеры
        public static Rect GetVisibleWorldRect(Camera camera)
        {
            float height = camera.orthographicSize * 2f;
            float width = height * camera.aspect;
            Vector3 p = camera.transform.position;
            return new Rect(p.x - width * 0.5f, p.y - height * 0.5f, width, height);
        }
    }
}
