using UnityEngine;

namespace Tanks2D
{
    // Ужимает RectTransform в безопасную зону экрана (вырезы, скругления, системные панели телефонов).
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaFitter : MonoBehaviour
    {
        private RectTransform _rect;
        private Rect _lastSafeArea;
        private Vector2Int _lastScreenSize;

        private void Awake()
        {
            _rect = GetComponent<RectTransform>();
            Apply();
        }

        private void Update()
        {
            if (Screen.safeArea != _lastSafeArea || _lastScreenSize.x != Screen.width || _lastScreenSize.y != Screen.height)
            {
                Apply();
            }
        }

        private void Apply()
        {
            if (Screen.width <= 0 || Screen.height <= 0) return;

            Rect safeArea = Screen.safeArea;
            _lastSafeArea = safeArea;
            _lastScreenSize = new Vector2Int(Screen.width, Screen.height);

            var screenSize = new Vector2(Screen.width, Screen.height);
            _rect.anchorMin = safeArea.position / screenSize;
            _rect.anchorMax = (safeArea.position + safeArea.size) / screenSize;
            _rect.offsetMin = Vector2.zero;
            _rect.offsetMax = Vector2.zero;
        }
    }
}
