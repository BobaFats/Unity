using TMPro;
using UnityEngine;

namespace Tanks2D
{
    // "Слот" для внешнего вида игрового объекта. Логика (коллайдеры, скрипты) живёт на корне,
    // а картинка — в дочернем объекте "Visual", который заполняется из VisualCatalog:
    // назначен Sprite -> рисуется арт, нет -> цветная фигура-заглушка с подписью.
    [DisallowMultipleComponent]
    public class VisualSlot : MonoBehaviour
    {
        private const string VisualChildName = "Visual";
        private const string LabelChildName = "Label";

        [SerializeField] private VisualId _id;
        [SerializeField] private int _sortingOrder;
        [SerializeField] private bool _showLabel = true;

        private SpriteRenderer _renderer;
        private Animator _animator;

        public VisualId Id => _id;
        public SpriteRenderer Renderer => _renderer;
        public Vector2 Size { get; private set; } = Vector2.one;

        // Аниматор есть только если в каталоге назначен контроллер
        public Animator Animator => _animator != null && _animator.runtimeAnimatorController != null ? _animator : null;

        private void Awake()
        {
            Apply();
        }

        public void Configure(VisualId id, int sortingOrder, bool showLabel = true)
        {
            _id = id;
            _sortingOrder = sortingOrder;
            _showLabel = showLabel;
            Apply();
        }

        [ContextMenu("Apply Visual")]
        public void Apply()
        {
            VisualEntry entry = VisualCatalog.Resolve(_id);
            Size = entry.size;

            Transform body = GetOrCreateChild(VisualChildName);
            _renderer = body.GetComponent<SpriteRenderer>();
            if (_renderer == null) _renderer = body.gameObject.AddComponent<SpriteRenderer>();

            _renderer.sprite = entry.HasArt ? entry.sprite : PlaceholderSprites.Get(entry.shape);
            _renderer.color = entry.HasArt ? Color.white : entry.color;
            _renderer.sortingOrder = _sortingOrder;

            FitToSize(body, entry);
            ApplyAnimator(body, entry);
            ApplyLabel(entry);
        }

        private void FitToSize(Transform body, VisualEntry entry)
        {
            body.localPosition = Vector3.zero;
            body.localRotation = Quaternion.identity;

            Vector2 spriteSize = _renderer.sprite != null ? (Vector2)_renderer.sprite.bounds.size : Vector2.one;
            if (spriteSize.x <= 0f || spriteSize.y <= 0f)
            {
                body.localScale = Vector3.one;
                return;
            }

            float scaleX = entry.size.x / spriteSize.x;
            float scaleY = entry.size.y / spriteSize.y;

            // Арт вписывается с сохранением пропорций, заглушка растягивается точно в размер
            if (entry.HasArt)
            {
                float uniform = Mathf.Min(scaleX, scaleY);
                scaleX = uniform;
                scaleY = uniform;
            }

            body.localScale = new Vector3(scaleX, scaleY, 1f);
        }

        private void ApplyAnimator(Transform body, VisualEntry entry)
        {
            _animator = body.GetComponent<Animator>();

            if (entry.animator != null)
            {
                if (_animator == null) _animator = body.gameObject.AddComponent<Animator>();
                _animator.runtimeAnimatorController = entry.animator;
            }
            else if (_animator != null)
            {
                _animator.runtimeAnimatorController = null;
            }
        }

        private void ApplyLabel(VisualEntry entry)
        {
            bool showLabel = _showLabel && !entry.HasArt && !string.IsNullOrEmpty(entry.label);
            Transform labelTransform = transform.Find(LabelChildName);

            if (!showLabel)
            {
                if (labelTransform != null) labelTransform.gameObject.SetActive(false);
                return;
            }

            if (labelTransform == null) labelTransform = GetOrCreateChild(LabelChildName);
            labelTransform.gameObject.SetActive(true);
            labelTransform.localPosition = new Vector3(0f, 0f, -0.01f);
            labelTransform.localRotation = Quaternion.identity;
            labelTransform.localScale = Vector3.one;

            TextMeshPro label = labelTransform.GetComponent<TextMeshPro>();
            if (label == null) label = labelTransform.gameObject.AddComponent<TextMeshPro>();

            label.text = entry.label;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.enableAutoSizing = true;
            label.fontSizeMin = 0.5f;
            label.fontSizeMax = 4f;
            label.fontStyle = FontStyles.Bold;
            label.color = new Color(0.08f, 0.08f, 0.08f, 1f);
            label.rectTransform.sizeDelta = entry.size * 0.85f;
            label.sortingOrder = _sortingOrder + 1;
        }

        private Transform GetOrCreateChild(string childName)
        {
            Transform child = transform.Find(childName);
            if (child != null) return child;

            var go = new GameObject(childName);
            go.transform.SetParent(transform, false);
            return go.transform;
        }
    }
}
