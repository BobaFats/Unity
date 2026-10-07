using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Tanks2D
{
    public static class PointerInput
    {
        private static readonly List<RaycastResult> _hits = new List<RaycastResult>();

        // Находится ли экранная точка над элементом UI (работает и для мыши, и для касаний)
        public static bool IsOverUI(Vector2 screenPosition)
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null) return false;

            var data = new PointerEventData(eventSystem) { position = screenPosition };
            _hits.Clear();
            eventSystem.RaycastAll(data, _hits);
            return _hits.Count > 0;
        }
    }
}
