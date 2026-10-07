using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Tanks2D
{
    // То же, что VisualSlot, но для UI-иконок (Image). Арт берётся из VisualCatalog.
    [RequireComponent(typeof(Image))]
    [DisallowMultipleComponent]
    public class UIVisualSlot : MonoBehaviour
    {
        private const string LabelChildName = "Label";

        [SerializeField] private VisualId _id;
        [SerializeField] private bool _showLabel = true;

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

            image.sprite = entry.HasArt ? entry.sprite : PlaceholderSprites.Get(entry.shape);
            image.color = entry.HasArt ? Color.white : entry.color;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.raycastTarget = false;

            bool showLabel = _showLabel && !entry.HasArt && !string.IsNullOrEmpty(entry.label);
            Transform labelTransform = transform.Find(LabelChildName);

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

            label.text = entry.label;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.enableAutoSizing = true;
            label.fontSizeMin = 8f;
            label.fontSizeMax = 40f;
            label.fontStyle = FontStyles.Bold;
            label.color = new Color(0.08f, 0.08f, 0.08f, 1f);
            label.raycastTarget = false;
        }
    }
}
