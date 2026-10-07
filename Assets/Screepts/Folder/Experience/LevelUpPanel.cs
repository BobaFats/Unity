using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Tanks2D
{
    // Окно выбора награды за уровень. Компонент висит на всегда активном объекте,
    // а показывается/прячется дочерняя панель _root.
    public class LevelUpPanel : MonoBehaviour
    {
        [Serializable]
        public class OptionView
        {
            public LevelUpOption option;
            public Button button;
            public TextMeshProUGUI text;
        }

        [SerializeField] private GameObject _root;
        [SerializeField] private TextMeshProUGUI _title;
        [SerializeField] private OptionView[] _options = Array.Empty<OptionView>();

        private Action<LevelUpOption> _onChosen;

        public bool IsOpen => _root != null && _root.activeSelf;

        private void Awake()
        {
            foreach (OptionView view in _options)
            {
                if (view.button == null) continue;
                LevelUpOption option = view.option;
                view.button.onClick.AddListener(() => Choose(option));
            }

            if (_root != null) _root.SetActive(false);
        }

        public void Show(int level, Func<LevelUpOption, bool> isAvailable, Func<LevelUpOption, string> describe, Action<LevelUpOption> onChosen)
        {
            _onChosen = onChosen;

            if (_title != null) _title.text = $"НОВЫЙ УРОВЕНЬ {level}!\n<size=60%>Выберите награду</size>";

            foreach (OptionView view in _options)
            {
                if (view.button == null) continue;

                bool available = isAvailable(view.option);
                view.button.gameObject.SetActive(available);
                if (available && view.text != null) view.text.text = describe(view.option);
            }

            if (_root != null) _root.SetActive(true);
        }

        public void Hide()
        {
            if (_root != null) _root.SetActive(false);
        }

        private void Choose(LevelUpOption option)
        {
            Action<LevelUpOption> callback = _onChosen;
            _onChosen = null;
            callback?.Invoke(option);
        }
    }
}
