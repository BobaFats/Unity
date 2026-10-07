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
        [Tooltip("Сцена, куда ведут «Новая игра» и «Продолжить» (лобби)")]
        [SerializeField] private string gameplaySceneName = "Lobby";

        private void Start()
        {
            GamePause.Clear();

            if (newGameButton != null) newGameButton.onClick.AddListener(StartNewGame);
            if (loadGameButton != null) loadGameButton.onClick.AddListener(ContinueGame);
            if (settingsButton != null) settingsButton.onClick.AddListener(OpenSettings);
        }

        private void StartNewGame()
        {
            GameStats.ResetAll();
            SceneManager.LoadScene(gameplaySceneName);
        }

        // Продолжить текущий забег (прогресс хранится, пока игра запущена; сохранение на диск — позже)
        private void ContinueGame()
        {
            SceneManager.LoadScene(gameplaySceneName);
        }

        private void OpenSettings()
        {
            Debug.Log("[Menu] Кнопка 'Настройки' нажата! (Окно настроек мы сделаем на следующем этапе)");
        }
    }
}
