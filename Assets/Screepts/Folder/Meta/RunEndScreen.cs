using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Tanks2D
{
    // Экран итогов забега (сцена RunEnd): победа или конец похода, статистика, заработанная слава.
    public class RunEndScreen : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _title;
        [SerializeField] private TextMeshProUGUI _stats;
        [SerializeField] private TextMeshProUGUI _glory;
        [SerializeField] private Button _menuButton;
        [SerializeField] private string _menuScene = "Menu";

        public RunResult Result { get; private set; }

        private void Start()
        {
            GamePause.Clear();
            if (_menuButton != null) _menuButton.onClick.AddListener(ToMenu);

            // Открыли сцену напрямую (в редакторе) — показываем итоги текущего прогресса без начисления славы
            Result = RunSummary.LastResult ?? RunSummary.Calculate(false);
            Show(Result);
        }

        private void Show(RunResult result)
        {
            if (_title != null) _title.text = result.victory ? "<color=#FFD24A>ПОБЕДА!</color>" : "ПОХОД ОКОНЧЕН";

            if (_stats != null)
            {
                _stats.text =
                    $"Пройдено уровней: <b>{result.levels}</b> из {GameStats.TotalLevels}\n" +
                    $"Побеждено боссов: <b>{result.bosses}</b>\n" +
                    $"Убито врагов: <b>{result.kills}</b>\n" +
                    $"Посещено городов: <b>{result.towns}</b>\n" +
                    $"Выполнено заданий: <b>{result.quests}</b>\n" +
                    $"Поражений: <b>{result.defeats}</b>\n" +
                    $"Время в походе: <b>{RunSummary.FormatTime(result.playTime)}</b>" +
                    (result.newRecord ? "\n\n<color=#5BE06A>Новый рекорд!</color>" : "");
            }

            if (_glory != null) _glory.text = $"Слава за поход: <b>+{result.glory}</b>\nВсего славы: {MetaProgress.Glory}";
        }

        public void ToMenu()
        {
            GamePause.Clear();
            SceneManager.LoadScene(_menuScene);
        }
    }
}
