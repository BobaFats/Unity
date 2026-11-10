using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace Tanks2D
{
    public class MainMenuController : MonoBehaviour
    {
        [Header("UI Buttons")]
        [SerializeField] private Button newGameButton;
        [SerializeField] private Button loadGameButton;
        [SerializeField] private Button settingsButton;

        [Header("Scene Settings")]
        [Tooltip("Сцена, куда ведёт «Новая игра» (лобби)")]
        [SerializeField] private string gameplaySceneName = "Lobby";

        public bool CanContinue => SaveSystem.HasRun;

        private void Start()
        {
            GamePause.Clear();

            if (newGameButton != null) newGameButton.onClick.AddListener(StartNewGame);
            if (loadGameButton != null)
            {
                loadGameButton.onClick.AddListener(ContinueGame);
                // «Продолжить» доступно, только если есть сохранённый забег
                loadGameButton.interactable = CanContinue;
            }
            if (settingsButton != null) settingsButton.onClick.AddListener(OpenSettings);
        }

        // Новый забег: прогресс с нуля + купленные улучшения наследия; старое сохранение забега удаляется
        public void StartNewGame()
        {
            GameStats.ResetAll();
            if (MetaUpgradeCatalog.Instance != null) MetaUpgradeCatalog.Instance.ApplyToNewRun();
            SaveSystem.DeleteRun();
            SceneManager.LoadScene(gameplaySceneName);
        }

        // Продолжить сохранённый забег с того экрана, где он был сохранён
        public void ContinueGame()
        {
            string scene = SaveSystem.LoadRun();
            if (string.IsNullOrEmpty(scene)) return;
            SceneManager.LoadScene(scene);
        }

        private void OpenSettings()
        {
            Debug.Log("[Menu] Кнопка 'Настройки' нажата! (Окно настроек мы сделаем на следующем этапе)");
        }
    }
}
