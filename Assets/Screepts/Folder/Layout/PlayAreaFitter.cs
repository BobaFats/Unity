using UnityEngine;

namespace Tanks2D
{
    // Вертикальная "колонка" для UI: на ПК (широкий экран) интерфейс не расползается по краям,
    // а стоит по центру над игровым полем; на телефоне занимает всю ширину.
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public class PlayAreaFitter : MonoBehaviour
    {
        [Tooltip("Соотношение ширина/высота колонки (9:16 = 0.5625)")]
        [SerializeField] private float _aspect = 9f / 16f;

        private RectTransform _rect;

        private void LateUpdate()
        {
            if (_rect == null) _rect = GetComponent<RectTransform>();
            var parent = _rect.parent as RectTransform;
            if (parent == null) return;

            Vector2 parentSize = parent.rect.size;
            float width = Mathf.Min(parentSize.x, parentSize.y * _aspect);
            if (Mathf.Approximately(_rect.sizeDelta.x, width) && _rect.anchorMin == new Vector2(0.5f, 0f)) return;

            _rect.anchorMin = new Vector2(0.5f, 0f);
            _rect.anchorMax = new Vector2(0.5f, 1f);
            _rect.pivot = new Vector2(0.5f, 0.5f);
            _rect.anchoredPosition = Vector2.zero;
            _rect.sizeDelta = new Vector2(width, 0f);
        }
    }
}
