using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Tanks2D
{
    // Окно выбора награды. Кнопки создаются из шаблона под каждый вариант.
    // Компонент висит на всегда активном объекте, показывается/прячется дочерняя панель _root.
    public class LevelUpPanel : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private TextMeshProUGUI _title;
        [Tooltip("Шаблон кнопки варианта: внутри TextMeshProUGUI и (необязательно) UIVisualSlot для иконки")]
        [SerializeField] private Button _optionTemplate;
        [SerializeField] private Transform _optionsContainer;

        private readonly List<Button> _buttons = new List<Button>();
        private List<UpgradeChoice> _choices = new List<UpgradeChoice>();
        private Action<UpgradeChoice> _onChosen;

        public bool IsOpen => _root != null && _root.activeSelf;
        public IReadOnlyList<UpgradeChoice> CurrentChoices => _choices;

        private void Awake()
        {
            if (_optionTemplate != null) _optionTemplate.gameObject.SetActive(false);
            if (_root != null) _root.SetActive(false);
        }

        public void Show(string title, List<UpgradeChoice> choices, Action<UpgradeChoice> onChosen)
        {
            _choices = choices;
            _onChosen = onChosen;

            if (_title != null) _title.text = title;

            foreach (Button button in _buttons) Destroy(button.gameObject);
            _buttons.Clear();

            for (int i = 0; i < choices.Count; i++)
            {
                UpgradeChoice choice = choices[i];
                Button button = Instantiate(_optionTemplate, _optionsContainer != null ? _optionsContainer : _optionTemplate.transform.parent);
                button.name = $"Option_{choice.Id}";
                button.gameObject.SetActive(true);

                TextMeshProUGUI text = button.GetComponentInChildren<TextMeshProUGUI>(true);
                if (text != null) text.text = $"<b>{choice.Title}</b>\n<size=80%>{choice.Description}</size>";

                UIVisualSlot icon = button.GetComponentInChildren<UIVisualSlot>(true);
                if (icon != null) icon.Configure(choice.Icon);

                int index = i;
                button.onClick.AddListener(() => Choose(index));
                _buttons.Add(button);
            }

            if (_root != null) _root.SetActive(true);
        }

        public void Hide()
        {
            if (_root != null) _root.SetActive(false);
        }

        public void Choose(int index)
        {
            if (index < 0 || index >= _choices.Count) return;

            Action<UpgradeChoice> callback = _onChosen;
            _onChosen = null;
            callback?.Invoke(_choices[index]);
        }
    }
}
