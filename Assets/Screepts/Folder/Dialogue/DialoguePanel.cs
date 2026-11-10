using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Tanks2D
{
    public enum DialogueLineKind
    {
        Speech,     // реплика: окно с портретом и именем говорящего
        Narration   // титр: текст по центру тёмного экрана («Спустя 2 часа...»), без имени и портрета
    }

    [Serializable]
    public class DialogueLine
    {
        [Tooltip("Реплика — окно с портретом и именем. Титр — текст по центру тёмного экрана («Спустя 2 часа...»): имя и портрет не нужны")]
        public DialogueLineKind kind;
        public string speaker;
        [Tooltip("Портрет говорящего (из VisualCatalog)")]
        public VisualId portrait = VisualId.Player;
        [Tooltip("Текст. Ремарки можно выделять курсивом: <i>(в мыслях)</i>")]
        [TextArea] public string text;
        [Tooltip("Портрет справа (обычно герой), иначе слева")]
        public bool rightSide;
        [Tooltip("Свой портрет (важнее картинки из каталога)")]
        public Sprite portraitSprite;

        public bool IsNarration => kind == DialogueLineKind.Narration;

        public DialogueLine Clone() => (DialogueLine)MemberwiseClone();
    }

    // Окно диалога: портрет, имя, текст с эффектом печатной машинки. Титры — отдельной карточкой на весь экран.
    // Игра на паузе, пока окно открыто. Далее — тап/клик по экрану, пробел или Enter.
    public class DialoguePanel : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private TextMeshProUGUI _speakerText;
        [SerializeField] private TextMeshProUGUI _lineText;
        [SerializeField] private UIVisualSlot _leftPortrait;
        [SerializeField] private UIVisualSlot _rightPortrait;
        [Tooltip("Кнопка на весь экран: нажатие — следующая реплика")]
        [SerializeField] private Button _nextButton;
        [SerializeField] private float _charactersPerSecond = 30f;

        [Header("Титры (не заданы — титр идёт в обычном окне без имени и портрета)")]
        [Tooltip("Карточка титра на весь экран")]
        [SerializeField] private GameObject _narrationRoot;
        [SerializeField] private TextMeshProUGUI _narrationText;
        [Tooltip("Окно реплик — скрывается, пока на экране титр")]
        [SerializeField] private GameObject _speechRoot;

        private readonly List<DialogueLine> _lines = new List<DialogueLine>();
        private int _index;
        private float _visibleCharacters;
        private int _totalCharacters;
        private TextMeshProUGUI _activeText;
        private Action _onComplete;

        public bool IsOpen => _root != null && _root.activeSelf;
        public DialogueLine CurrentLine => IsOpen && _index < _lines.Count ? _lines[_index] : null;
        public bool HasNarrationCard => _narrationRoot != null && _narrationText != null;
        // Сейчас на экране карточка титра (а не окно реплик)
        public bool IsNarrationShown => IsOpen && HasNarrationCard && _narrationRoot.activeSelf;
        private bool IsTyping => _activeText != null && _visibleCharacters < _totalCharacters;

        private void Awake()
        {
            if (_nextButton != null) _nextButton.onClick.AddListener(Next);
            if (_root != null) _root.SetActive(false);
        }

        public void Play(IList<DialogueLine> lines, Action onComplete)
        {
            _lines.Clear();
            if (lines != null)
            {
                foreach (DialogueLine line in lines) if (line != null) _lines.Add(line);
            }
            _onComplete = onComplete;
            _index = 0;

            if (_lines.Count == 0 || _root == null)
            {
                _onComplete = null;
                onComplete?.Invoke();
                return;
            }

            _root.SetActive(true);
            GamePause.Set(this, true);
            ShowLine();
        }

        private void Update()
        {
            if (!IsOpen) return;

            if (IsTyping)
            {
                // Печатаем в реальном времени: игра стоит на паузе
                _visibleCharacters += _charactersPerSecond * Time.unscaledDeltaTime;
                _activeText.maxVisibleCharacters = Mathf.FloorToInt(_visibleCharacters);
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame)) Next();
        }

        // Первое нажатие допечатывает реплику, второе — переходит к следующей
        public void Next()
        {
            if (!IsOpen) return;

            if (IsTyping)
            {
                _visibleCharacters = _totalCharacters;
                _activeText.maxVisibleCharacters = _totalCharacters;
                return;
            }

            _index++;
            if (_index < _lines.Count) ShowLine();
            else Close();
        }

        private void ShowLine()
        {
            DialogueLine line = _lines[_index];
            bool narration = line.IsNarration;
            // Титр — на своей карточке; если её в сцене нет, показываем в окне реплик без имени и портрета
            bool card = narration && HasNarrationCard;

            if (_narrationRoot != null) _narrationRoot.SetActive(card);
            if (_speechRoot != null) _speechRoot.SetActive(!card);

            if (!card)
            {
                if (_speakerText != null)
                {
                    _speakerText.text = narration ? "" : line.speaker;
                    _speakerText.alignment = line.rightSide ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft;
                }

                SetPortrait(_leftPortrait, !narration && !line.rightSide, line.portrait, line.portraitSprite);
                SetPortrait(_rightPortrait, !narration && line.rightSide, line.portrait, line.portraitSprite);
            }

            _activeText = card ? _narrationText : _lineText;
            _visibleCharacters = 0f;
            _totalCharacters = 0;

            if (_activeText != null)
            {
                _activeText.text = line.text;
                // Считаем видимые символы, а не длину строки: теги оформления (<i>, <b>) не печатаются
                _activeText.ForceMeshUpdate(true);
                _totalCharacters = _activeText.textInfo != null ? _activeText.textInfo.characterCount : (line.text ?? "").Length;
                _activeText.maxVisibleCharacters = 0;
            }
        }

        private static void SetPortrait(UIVisualSlot slot, bool visible, VisualId id, Sprite sprite)
        {
            if (slot == null) return;
            slot.gameObject.SetActive(visible);
            if (visible) slot.Configure(id, sprite);
        }

        private void Close()
        {
            _root.SetActive(false);
            GamePause.Set(this, false);

            Action callback = _onComplete;
            _onComplete = null;
            callback?.Invoke();
        }

        private void OnDestroy()
        {
            GamePause.Set(this, false);
        }
    }
}
