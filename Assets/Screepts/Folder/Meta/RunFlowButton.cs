using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Tanks2D
{
    // Кнопки хода забега:
    //   RetryLevel — после поражения: прогресс откатывается к сохранению перед боем, бой начинается заново;
    //   FinishRun  — завершить поход: итоги, слава в наследие (нужно нажать дважды — защита от случайного нажатия).
    [RequireComponent(typeof(Button))]
    public class RunFlowButton : MonoBehaviour
    {
        public enum Action { RetryLevel, FinishRun }

        [SerializeField] private Action _action;
        [SerializeField] private string _battleScene = "Battle";
        [SerializeField] private float _confirmSeconds = 3f;

        private TextMeshProUGUI _label;
        private string _normalText;
        private float _confirmUntil = -1f;

        public Action Kind => _action;
        public bool IsAwaitingConfirm => Time.unscaledTime < _confirmUntil;

        private void Start()
        {
            GetComponent<Button>().onClick.AddListener(Press);
            _label = GetComponentInChildren<TextMeshProUGUI>(true);
            if (_label != null) _normalText = _label.text;
        }

        private void Update()
        {
            if (_confirmUntil > 0f && !IsAwaitingConfirm)
            {
                _confirmUntil = -1f;
                if (_label != null) _label.text = _normalText;
            }
        }

        public void Press()
        {
            if (_action == Action.RetryLevel)
            {
                RetryLevel(_battleScene);
                return;
            }

            if (!IsAwaitingConfirm)
            {
                _confirmUntil = Time.unscaledTime + _confirmSeconds;
                if (_label != null) _label.text = "Точно? Нажмите ещё раз";
                return;
            }

            RunSummary.Finish(false);
        }

        public static void RetryLevel(string battleScene = "Battle")
        {
            // Сохранение пишем, только если оно от этого же забега (иначе бой запущен в редакторе напрямую)
            bool reverted = SaveSystem.RevertUnfinishedBattle();
            GameStats.RunDefeats++;
            if (reverted) SaveSystem.SaveRun("Lobby");

            GamePause.Clear();
            SceneManager.LoadScene(battleScene);
        }
    }
}
