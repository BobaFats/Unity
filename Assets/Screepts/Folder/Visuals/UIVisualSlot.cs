using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Tanks2D
{
    // То же, что VisualSlot, но для UI-иконок (Image).
    // Приоритет: «Свой спрайт» -> спрайт из VisualCatalog -> спрайт, перетащенный вручную в Image -> заглушка.
    // Текст подписи можно править прямо в дочернем объекте "Label" или в поле «Свой текст подписи».
    [RequireComponent(typeof(Image))]
    [DisallowMultipleComponent]
    public class UIVisualSlot : MonoBehaviour
    {
        private const string LabelChildName = "Label";

        [SerializeField] private VisualId _id;
        [SerializeField] private bool _showLabel = true;

        [Header("Своё оформление этой иконки (важнее каталога)")]
        [SerializeField] private Sprite _customSprite;
        [Tooltip("Текст подписи вместо текста из каталога. Если задан — подпись видна и поверх арта")]
        [SerializeField] private string _customLabel;

        [SerializeField, HideInInspector] private string _lastWrittenLabel;
        [SerializeField, HideInInspector] private Sprite _lastAppliedSprite;

        private void Awake()
        {
            Apply();
        }

        public void Configure(VisualId id, bool showLabel = true)
        {
            _id = id;
            _showLabel = showLabel;
            Apply();
        }

        [ContextMenu("Apply Visual")]
        public void Apply()
        {
            VisualEntry entry = VisualCatalog.Resolve(_id);
            Image image = GetComponent<Image>();

            Sprite current = image.sprite;
            // Ручной спрайт — тот, что поставил не этот скрипт
            bool manual = current != null && current != _lastAppliedSprite && !PlaceholderSprites.IsPlaceholder(current) && current != entry.sprite;

            // Спрайт перетащили руками в Image — запоминаем как свой, чтобы его больше ничего не затирало
            if (manual && _customSprite == null) _customSprite = current;

            Sprite art = _customSprite != null ? _customSprite : entry.sprite;

            image.sprite = art != null ? art : PlaceholderSprites.Get(entry.shape);
            _lastAppliedSprite = image.sprite;
            image.color = art != null ? Color.white : entry.color;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.raycastTarget = false;

            ApplyLabel(entry, art != null);
        }

        private void ApplyLabel(VisualEntry entry, bool hasArt)
        {
            Transform labelTransform = transform.Find(LabelChildName);
            TextMeshProUGUI existing = labelTransform != null ? labelTransform.GetComponent<TextMeshProUGUI>() : null;

            // Текст подписи поправили руками в объекте Label — запоминаем как свой
            if (existing != null && !string.IsNullOrEmpty(_lastWrittenLabel) && existing.text != _lastWrittenLabel)
            {
                _customLabel = existing.text;
            }

            string text = !string.IsNullOrEmpty(_customLabel) ? _customLabel : entry.label;
            bool showLabel = _showLabel && !string.IsNullOrEmpty(text) && (!hasArt || !string.IsNullOrEmpty(_customLabel));

            if (!showLabel)
            {
                if (labelTransform != null) labelTransform.gameObject.SetActive(false);
                return;
            }

            if (labelTransform == null)
            {
                var go = new GameObject(LabelChildName, typeof(RectTransform));
                go.layer = gameObject.layer;
                go.transform.SetParent(transform, false);
                labelTransform = go.transform;
            }

            labelTransform.gameObject.SetActive(true);
            var rect = (RectTransform)labelTransform;
            rect.anchorMin = new Vector2(0.1f, 0.1f);
            rect.anchorMax = new Vector2(0.9f, 0.9f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            TextMeshProUGUI label = labelTransform.GetComponent<TextMeshProUGUI>();
            if (label == null) label = labelTransform.gameObject.AddComponent<TextMeshProUGUI>();

            label.text = text;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.enableAutoSizing = true;
            label.fontSizeMin = 8f;
            label.fontSizeMax = 40f;
            label.fontStyle = FontStyles.Bold;
            label.color = new Color(0.08f, 0.08f, 0.08f, 1f);
            label.raycastTarget = false;

            _lastWrittenLabel = text;
        }

#if UNITY_EDITOR
        // Изменения в инспекторе видны сразу, без запуска игры
        private void OnValidate()
        {
            if (Application.isPlaying) return;
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null || Application.isPlaying) return;
                if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(this)) return;
                Apply();
            };
        }
#endif
    }
}
