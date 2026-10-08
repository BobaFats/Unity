using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

namespace Tanks2D
{
    // Горизонтальная стена над героем. Враги останавливаются у её верхнего края (TopY).
    public class Wall : MonoBehaviour
    {
        [Header("Visual")]
        [SerializeField] private VisualSlot _visual;
        [Tooltip("Анимации стены: Idle, Hit (удар), Death (разрушена). Пусто — берутся с этого же объекта")]
        [SerializeField] private CharacterAnimations _animations;

        [Header("UI Elements & Colors")]
        [SerializeField] private Slider wallHPSlider;
        [SerializeField] private Gradient hpGradient;
        [SerializeField] private TextMeshProUGUI wallHPText;

        [Header("Damage Text Settings (Всплывающий урон стены)")]
        [SerializeField] private GameObject damageTextPrefab;
        [SerializeField] private Color textColorForWall = new Color(0.75f, 0.35f, 1f);

        [Header("Animation Settings")]
        [SerializeField] private float lerpSpeed = 5f;

        [Header("Game Over Settings (Экран конца игры)")]
        [SerializeField] private GameObject gameOverPanel;
        [SerializeField] private Button restartButton;

        private int _currentHP;
        private float _visualHP;
        private Image _sliderFillImage;

        public static Wall ActiveInstance { get; private set; }
        public static bool IsGameOver { get; private set; }

        // Верх и ширина берутся по реальной картинке (с учётом её сдвига/масштаба), если она есть
        private float _topOffset = float.NaN;
        private float _halfWidth = float.NaN;

        public float TopY
        {
            get
            {
                if (float.IsNaN(_topOffset)) MeasureVisual();
                return transform.position.y + _topOffset;
            }
        }

        public float HalfWidth
        {
            get
            {
                if (float.IsNaN(_halfWidth)) MeasureVisual();
                return _halfWidth;
            }
        }

        private void MeasureVisual()
        {
            if (_visual != null && _visual.TryGetWorldBounds(out Bounds bounds))
            {
                _topOffset = bounds.max.y - transform.position.y;
                _halfWidth = bounds.extents.x;
                return;
            }

            _topOffset = _visual != null ? _visual.Size.y * 0.5f * transform.lossyScale.y : 0.3f;
            _halfWidth = _visual != null ? _visual.Size.x * 0.5f * transform.lossyScale.x : 4.5f;
        }

        private void Awake()
        {
            IsGameOver = false;
        }

        // OnEnable, а не Awake: переживает перезагрузку скриптов прямо в Play Mode
        private void OnEnable()
        {
            ActiveInstance = this;
        }

        private void OnDestroy()
        {
            GamePause.Set(this, false);
            if (ActiveInstance == this) ActiveInstance = null;
        }

        private void Start()
        {
            if (_animations == null) _animations = GetComponent<CharacterAnimations>();

            _currentHP = GameStats.WallMaxHP;
            _visualHP = _currentHP;

            if (wallHPSlider != null)
            {
                wallHPSlider.minValue = 0;
                wallHPSlider.maxValue = GameStats.WallMaxHP;
                wallHPSlider.value = _visualHP;
                if (wallHPSlider.fillRect != null) _sliderFillImage = wallHPSlider.fillRect.GetComponent<Image>();
            }

            UpdateSliderVisuals();

            if (restartButton != null) restartButton.onClick.AddListener(RestartGame);
            if (gameOverPanel != null) gameOverPanel.SetActive(false);
        }

        private void Update()
        {
            if (Mathf.Approximately(_visualHP, _currentHP)) return;

            // unscaledDeltaTime — чтобы полоска доезжала и на экране Game Over
            _visualHP = Mathf.MoveTowards(_visualHP, _currentHP, lerpSpeed * GameStats.WallMaxHP * Time.unscaledDeltaTime);
            if (wallHPSlider != null) wallHPSlider.value = _visualHP;
            UpdateSliderVisuals();
        }

        public void TakeDamage(int damageAmount)
        {
            if (IsGameOver) return;
            // Босс повержен — миссия выиграна, остатки врагов стену уже не ломают
            if (EnemySpawner2D.Instance != null && EnemySpawner2D.Instance.IsBossDefeated) return;

            _currentHP = Mathf.Max(0, _currentHP - damageAmount);

            if (damageTextPrefab != null)
            {
                // Цифра урона появляется в случайной точке по ширине стены
                float x = Random.Range(-HalfWidth * 0.8f, HalfWidth * 0.8f);
                Vector3 spawnPosition = new Vector3(transform.position.x + x, TopY + 0.3f, 0f);
                GameObject textGo = Instantiate(damageTextPrefab, spawnPosition, Quaternion.identity);

                DamageText damageText = textGo.GetComponent<DamageText>();
                if (damageText != null) damageText.Setup(damageAmount, textColorForWall);
            }

            UpdateHPText();

            if (_currentHP <= 0) TriggerGameOver();
            else if (_animations != null) _animations.Play(CharacterAnimation.Hit);
        }

        // Вызывается из магазина при покупке улучшения стены: растим максимум и полностью чиним
        public void UpgradeAndRepair()
        {
            _currentHP = GameStats.WallMaxHP;
            _visualHP = _currentHP;

            if (wallHPSlider != null)
            {
                wallHPSlider.maxValue = GameStats.WallMaxHP;
                wallHPSlider.value = _visualHP;
            }

            UpdateSliderVisuals();
        }

        private void UpdateSliderVisuals()
        {
            if (_sliderFillImage != null && hpGradient != null)
            {
                _sliderFillImage.color = hpGradient.Evaluate(_visualHP / Mathf.Max(1, GameStats.WallMaxHP));
            }
            UpdateHPText();
        }

        private void UpdateHPText()
        {
            if (wallHPText != null) wallHPText.text = $"{_currentHP} / {GameStats.WallMaxHP}";
        }

        private void TriggerGameOver()
        {
            Debug.Log("[Game Over] СТЕНА РАЗРУШЕНА!");
            IsGameOver = true;

            if (gameOverPanel != null) gameOverPanel.SetActive(true);
            if (_animations != null) _animations.Play(CharacterAnimation.Death);
            GamePause.Set(this, true);
        }

        public void RestartGame()
        {
            GamePause.Clear();
            IsGameOver = false;

            // Полный перезапуск: прогресс сбрасывается
            GameStats.ResetAll();

            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
}
