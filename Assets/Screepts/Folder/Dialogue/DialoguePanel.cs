using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Tanks2D
{
    [Serializable]
    public class DialogueLine
    {
        public string speaker;
        [Tooltip("Портрет говорящего (из VisualCatalog)")]
        public VisualId portrait = VisualId.Player;
        [TextArea] public string text;
        [Tooltip("Портрет справа (обычно герой), иначе слева")]
        public bool rightSide;
    }

    // Окно диалога: портрет, имя, текст с эффектом печатной машинки.
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

        private readonly List<DialogueLine> _lines = new List<DialogueLine>();
        private int _index;
        private float _visibleCharacters;
        private Action _onComplete;

        public bool IsOpen => _root != null && _root.activeSelf;
        public DialogueLine CurrentLine => IsOpen && _index < _lines.Count ? _lines[_index] : null;
        private bool IsTyping => _lineText != null && _visibleCharacters < _lineText.text.Length;

        private void Awake()
        {
            if (_nextButton != null) _nextButton.onClick.AddListener(Next);
            if (_root != null) _root.SetActive(false);
        }

        public void Play(IList<DialogueLine> lines, Action onComplete)
        {
            _lines.Clear();
            if (lines != null) _lines.AddRange(lines);
            _onComplete = onComplete;
            _index = 0;

            if (_lines.Count == 0 || _root == null)
            {
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
                _lineText.maxVisibleCharacters = Mathf.FloorToInt(_visibleCharacters);
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
                _visibleCharacters = _lineText.text.Length;
                _lineText.maxVisibleCharacters = _lineText.text.Length;
                return;
            }

            _index++;
            if (_index < _lines.Count) ShowLine();
            else Close();
        }

        private void ShowLine()
        {
            DialogueLine line = _lines[_index];

            if (_speakerText != null)
            {
                _speakerText.text = line.speaker;
                _speakerText.alignment = line.rightSide ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft;
            }

            SetPortrait(_leftPortrait, !line.rightSide, line.portrait);
            SetPortrait(_rightPortrait, line.rightSide, line.portrait);

            if (_lineText != null)
            {
                _lineText.text = line.text;
                _lineText.maxVisibleCharacters = 0;
            }
            _visibleCharacters = 0f;
        }

        private static void SetPortrait(UIVisualSlot slot, bool visible, VisualId id)
        {
            if (slot == null) return;
            slot.gameObject.SetActive(visible);
            if (visible) slot.Configure(id);
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
