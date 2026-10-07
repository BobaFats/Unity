using UnityEngine;
using UnityEngine.UI;

namespace Tanks2D
{
    // Масштаб UI под портретный макет 1080x1920:
    // экран уже макета (длинные телефоны) — подгоняем по ширине, шире (ПК, планшеты) — по высоте.
    [ExecuteAlways]
    [RequireComponent(typeof(CanvasScaler))]
    public class AdaptiveCanvasScaler : MonoBehaviour
    {
        [SerializeField] private Vector2 _referenceResolution = new Vector2(1080f, 1920f);

        private CanvasScaler _scaler;

        private void OnEnable()
        {
            _scaler = GetComponent<CanvasScaler>();
            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            _scaler.referenceResolution = _referenceResolution;
            UpdateMatch();
        }

        private void Update()
        {
            UpdateMatch();
        }

        private void UpdateMatch()
        {
            if (_scaler == null || Screen.height <= 0) return;

            float screenAspect = (float)Screen.width / Screen.height;
            float referenceAspect = _referenceResolution.x / _referenceResolution.y;
            _scaler.matchWidthOrHeight = screenAspect < referenceAspect ? 0f : 1f;
        }
    }
}
