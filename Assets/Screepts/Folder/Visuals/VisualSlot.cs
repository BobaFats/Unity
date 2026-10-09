using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

namespace Tanks2D
{
    // Внешний вид игрового объекта. Логика (коллайдеры, скрипты) живёт на корне, картинка — в дочерних объектах.
    //
    // Что показывается (по приоритету):
    //   1) «Своя модель» этого объекта (любой префаб: анимированный спрайт, 3D-модель)
    //   2) «Свой спрайт» этого объекта
    //   3) модель из VisualCatalog
    //   4) спрайт из VisualCatalog
    //   5) заглушка: цветная фигура с подписью
    // Спрайт, перетащенный вручную в SpriteRenderer объекта "Visual", автоматически становится «Своим спрайтом».
    //
    // Текст подписи можно править прямо в объекте "Label" или в поле «Свой текст подписи».
    // Положение картинки/модели: поля «Смещение / Поворот / Масштаб» или просто двигайте объект Visual (Model) в сцене —
    // ручной сдвиг запоминается в этих полях и больше не сбрасывается.
    [DisallowMultipleComponent]
    public class VisualSlot : MonoBehaviour
    {
        private const string VisualChildName = "Visual";
        private const string ModelChildName = "Model";
        private const string LabelChildName = "Label";

        [SerializeField] private VisualId _id;
        [SerializeField] private int _sortingOrder;
        [SerializeField] private bool _showLabel = true;

        [Header("Своё оформление этого объекта (важнее каталога)")]
        [Tooltip("Спрайт только для этого объекта")]
        [SerializeField] private Sprite _customSprite;
        [Tooltip("Модель только для этого объекта: любой префаб (анимированный спрайт, 3D-модель)")]
        [SerializeField] private GameObject _customPrefab;
        [Tooltip("Текст подписи вместо текста из каталога. Если задан — подпись видна и поверх арта")]
        [SerializeField] private string _customLabel;
        [Tooltip("Цвет-оттенок для своего арта (белый — без изменений)")]
        [SerializeField] private Color _customColor = Color.white;

        [Header("Положение картинки / модели относительно объекта")]
        [Tooltip("Вписывать картинку/модель в размер объекта из каталога")]
        [FormerlySerializedAs("_fitModelToSize")]
        [SerializeField] private bool _fitToSize = true;
        [FormerlySerializedAs("_modelScale")]
        [SerializeField] private float _scale = 1f;
        [FormerlySerializedAs("_modelOffset")]
        [SerializeField] private Vector3 _offset;
        [FormerlySerializedAs("_modelRotation")]
        [SerializeField] private Vector3 _rotation;

        [SerializeField, HideInInspector] private GameObject _modelSource;
        [SerializeField, HideInInspector] private string _lastTargetName;
        [SerializeField, HideInInspector] private Vector3 _lastPosition;
        [SerializeField, HideInInspector] private Quaternion _lastRotation = Quaternion.identity;
        [SerializeField, HideInInspector] private Vector3 _lastScale = Vector3.one;
        [SerializeField, HideInInspector] private string _lastWrittenLabel;
        [SerializeField, HideInInspector] private Sprite _lastAppliedSprite;

        private SpriteRenderer _bodyRenderer;
        private SpriteRenderer _renderer;
        private Animator _animator;

        public VisualId Id => _id;
        // Спрайт, который сейчас виден (для подсветки попаданий). У 3D-модели может быть null.
        public SpriteRenderer Renderer => _renderer;
        public Vector2 Size { get; private set; } = Vector2.one;
        public string CurrentSource { get; private set; } = "";

        // Объект с картинкой: Model, если подставлена модель, иначе Visual. На нём же играют Animation Clip-ы.
        public Transform ArtRoot
        {
            get
            {
                Transform model = transform.Find(ModelChildName);
                return model != null ? model : transform.Find(VisualChildName);
            }
        }

        // Реальные границы видимой картинки в мире (с учётом сдвига, поворота и масштаба)
        public bool TryGetWorldBounds(out Bounds bounds)
        {
            bounds = default;
            Transform root = ArtRoot;
            if (root == null) return false;

            bool found = false;
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || r is ParticleSystemRenderer) continue;
                if (!found) { bounds = r.bounds; found = true; }
                else bounds.Encapsulate(r.bounds);
            }
            return found;
        }

        // Аниматор есть только если назначен контроллер (в каталоге или внутри модели)
        public Animator Animator => _animator != null && _animator.runtimeAnimatorController != null ? _animator : null;

        private void Awake()
        {
            Apply();
        }

        // Для скинов и других систем, меняющих арт из кода
        public Sprite CustomSprite => _customSprite;
        public GameObject CustomPrefab => _customPrefab;
        public Color CustomColor => _customColor;
        public Vector3 Offset => _offset;
        public Vector3 Rotation => _rotation;
        public float Scale => _scale;

        public void SetArt(Sprite sprite, GameObject prefab, Color color)
        {
            _customSprite = sprite;
            _customPrefab = prefab;
            _customColor = color;
            _lastTargetName = null; // новый арт — не принимать прошлое положение за ручной сдвиг
            Apply();
        }

        public void SetPlacement(Vector3 offset, Vector3 rotation, float scale)
        {
            _offset = offset;
            _rotation = rotation;
            _scale = scale;
            _lastTargetName = null;
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
            _bodyRenderer = body.GetComponent<SpriteRenderer>();
            if (_bodyRenderer == null) _bodyRenderer = body.gameObject.AddComponent<SpriteRenderer>();
            _bodyRenderer.sortingOrder = _sortingOrder;

            GameObject prefab = _customPrefab != null ? _customPrefab : (_customSprite == null ? entry.prefab : null);
            bool hasArt;

            if (prefab != null)
            {
                ShowModel(prefab, entry);
                hasArt = true;
                CurrentSource = _customPrefab != null ? "своя модель" : "модель из каталога";
            }
            else
            {
                RemoveModel();
                hasArt = ShowSprite(body, entry);
            }

            ApplyAnimator(body, entry);
            ApplyLabel(entry, hasArt);
        }

        // ---------------------------------------------------------------- Спрайт

        private bool ShowSprite(Transform body, VisualEntry entry)
        {
            _bodyRenderer.enabled = true;
            _renderer = _bodyRenderer;

            Sprite current = _bodyRenderer.sprite;
            // Ручной спрайт — тот, что поставил не этот скрипт
            bool manual = current != null && current != _lastAppliedSprite && !PlaceholderSprites.IsPlaceholder(current) && current != entry.sprite;

            // Спрайт перетащили руками в SpriteRenderer — запоминаем как свой, чтобы его больше ничего не затирало
            if (manual && _customSprite == null) _customSprite = current;

            Sprite art;
            if (_customSprite != null) { art = _customSprite; CurrentSource = "свой спрайт"; }
            else if (entry.sprite != null) { art = entry.sprite; CurrentSource = "спрайт из каталога"; }
            else { art = null; CurrentSource = "заглушка"; }

            if (art != null)
            {
                _bodyRenderer.sprite = art;
                _bodyRenderer.color = _customColor;
            }
            else
            {
                _bodyRenderer.sprite = PlaceholderSprites.Get(entry.shape);
                _bodyRenderer.color = entry.color;
            }

            _lastAppliedSprite = _bodyRenderer.sprite;
            FitSprite(body, entry.size, art != null);
            return art != null;
        }

        private void FitSprite(Transform body, Vector2 size, bool keepAspect)
        {
            AdoptManualTransform(body);

            float scaleX = 1f;
            float scaleY = 1f;
            Vector2 spriteSize = _bodyRenderer.sprite != null ? (Vector2)_bodyRenderer.sprite.bounds.size : Vector2.one;

            if ((_fitToSize || !keepAspect) && spriteSize.x > 0f && spriteSize.y > 0f)
            {
                scaleX = size.x / spriteSize.x;
                scaleY = size.y / spriteSize.y;

                // Арт вписывается с сохранением пропорций, заглушка растягивается точно в размер
                if (keepAspect)
                {
                    float uniform = Mathf.Min(scaleX, scaleY);
                    scaleX = uniform;
                    scaleY = uniform;
                }
            }

            body.localPosition = _offset;
            body.localRotation = Quaternion.Euler(_rotation);
            body.localScale = new Vector3(scaleX * _scale, scaleY * _scale, 1f);
            RememberTransform(body);
        }

        // Объект Visual/Model сдвинули, повернули или масштабировали руками — переносим это в поля
        private void AdoptManualTransform(Transform target)
        {
            if (_lastTargetName != target.name) return;

            Vector3 positionDelta = target.localPosition - _lastPosition;
            if (positionDelta.sqrMagnitude > 0.000001f) _offset += positionDelta;

            if (Quaternion.Angle(target.localRotation, _lastRotation) > 0.01f)
            {
                _rotation = (target.localRotation * Quaternion.Inverse(_lastRotation) * Quaternion.Euler(_rotation)).eulerAngles;
            }

            if (Mathf.Abs(_lastScale.x) > 0.0001f && Mathf.Abs(target.localScale.x - _lastScale.x) > 0.0001f)
            {
                _scale *= target.localScale.x / _lastScale.x;
            }
        }

        private void RememberTransform(Transform target)
        {
            _lastTargetName = target.name;
            _lastPosition = target.localPosition;
            _lastRotation = target.localRotation;
            _lastScale = target.localScale;
        }

        // ---------------------------------------------------------------- Модель

        private void ShowModel(GameObject prefab, VisualEntry entry)
        {
            _bodyRenderer.enabled = false;

            Transform model = transform.Find(ModelChildName);
            if (model != null && _modelSource != prefab)
            {
                DestroySafe(model.gameObject);
                model = null;
            }

            if (model == null)
            {
                GameObject instance = Instantiate(prefab, transform);
                instance.name = ModelChildName;
                _lastTargetName = null;
                model = instance.transform;
                _modelSource = prefab;

                // Модель рисуется поверх фона и в порядке слоя этого объекта
                foreach (Renderer r in instance.GetComponentsInChildren<Renderer>(true)) r.sortingOrder += _sortingOrder;
            }

            FitModel(model, entry.size);

            _renderer = model.GetComponentInChildren<SpriteRenderer>(true);
            if (_renderer != null && _customColor != Color.white) _renderer.color = _customColor;
        }

        private void FitModel(Transform model, Vector2 size)
        {
            AdoptManualTransform(model);

            model.localRotation = Quaternion.Euler(_rotation);
            model.localScale = Vector3.one;
            model.localPosition = Vector3.zero;

            float scale = _scale;
            Vector3 centerOffset = Vector3.zero;

            if (_fitToSize && TryGetLocalBounds(model, out Bounds bounds) && bounds.size.x > 0.0001f && bounds.size.y > 0.0001f)
            {
                scale *= Mathf.Min(size.x / bounds.size.x, size.y / bounds.size.y);
                centerOffset = -bounds.center * scale;
            }

            model.localScale = Vector3.one * scale;
            model.localPosition = centerOffset + _offset;
            RememberTransform(model);
        }

        // Габариты модели в локальных координатах этого объекта (при масштабе модели 1)
        private bool TryGetLocalBounds(Transform model, out Bounds bounds)
        {
            bounds = default;
            bool found = false;

            foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) continue;

                Bounds world = r.bounds;
                Vector3 min = transform.InverseTransformPoint(world.min);
                Vector3 max = transform.InverseTransformPoint(world.max);
                var local = new Bounds((min + max) * 0.5f, new Vector3(Mathf.Abs(max.x - min.x), Mathf.Abs(max.y - min.y), Mathf.Abs(max.z - min.z)));

                if (!found) { bounds = local; found = true; }
                else bounds.Encapsulate(local);
            }

            return found;
        }

        private void RemoveModel()
        {
            Transform model = transform.Find(ModelChildName);
            if (model != null) DestroySafe(model.gameObject);
            _modelSource = null;
        }

        // ---------------------------------------------------------------- Аниматор и подпись

        private void ApplyAnimator(Transform body, VisualEntry entry)
        {
            Transform model = transform.Find(ModelChildName);
            Animator modelAnimator = model != null ? model.GetComponentInChildren<Animator>(true) : null;

            if (modelAnimator != null)
            {
                _animator = modelAnimator;
                if (entry.animator != null && modelAnimator.runtimeAnimatorController == null) modelAnimator.runtimeAnimatorController = entry.animator;
                return;
            }

            _animator = body.GetComponent<Animator>();

            if (entry.animator != null && model == null)
            {
                if (_animator == null) _animator = body.gameObject.AddComponent<Animator>();
                _animator.runtimeAnimatorController = entry.animator;
            }
            else if (_animator != null)
            {
                _animator.runtimeAnimatorController = null;
            }
        }

        private void ApplyLabel(VisualEntry entry, bool hasArt)
        {
            Transform labelTransform = transform.Find(LabelChildName);
            TextMeshPro existing = labelTransform != null ? labelTransform.GetComponent<TextMeshPro>() : null;

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

            if (labelTransform == null) labelTransform = GetOrCreateChild(LabelChildName);
            labelTransform.gameObject.SetActive(true);
            labelTransform.localPosition = new Vector3(0f, 0f, -0.01f);
            labelTransform.localRotation = Quaternion.identity;
            labelTransform.localScale = Vector3.one;

            TextMeshPro label = labelTransform.GetComponent<TextMeshPro>();
            if (label == null) label = labelTransform.gameObject.AddComponent<TextMeshPro>();

            label.text = text;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.enableAutoSizing = true;
            label.fontSizeMin = 0.5f;
            label.fontSizeMax = 4f;
            label.fontStyle = FontStyles.Bold;
            label.color = new Color(0.08f, 0.08f, 0.08f, 1f);
            label.rectTransform.sizeDelta = entry.size * 0.85f;
            label.sortingOrder = _sortingOrder + 1;

            _lastWrittenLabel = text;
        }

        // ---------------------------------------------------------------- Helpers

        private Transform GetOrCreateChild(string childName)
        {
            Transform child = transform.Find(childName);
            if (child != null) return child;

            var go = new GameObject(childName);
            go.transform.SetParent(transform, false);
            return go.transform;
        }

        private static void DestroySafe(GameObject go)
        {
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
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
