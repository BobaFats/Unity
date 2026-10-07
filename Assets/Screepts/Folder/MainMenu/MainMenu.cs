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
        [Tooltip("Имя боевой сцены, которая загрузится при нажатии Новая Игра")]
        [SerializeField] private string gameplaySceneName = "Battle";

        private void Start()
        {
            GamePause.Clear();

            if (newGameButton != null) newGameButton.onClick.AddListener(StartNewGame);
            if (loadGameButton != null) loadGameButton.onClick.AddListener(LoadSavedGame);
            if (settingsButton != null) settingsButton.onClick.AddListener(OpenSettings);
        }

        private void StartNewGame()
        {
            GameStats.ResetAll();
            Wallet.ResetWallet();
            SceneManager.LoadScene(gameplaySceneName);
        }

        private void LoadSavedGame()
        {
            Debug.Log("[Menu] Кнопка 'Загрузить игру' нажата! (Систему сохранений мы прикрутим чуть позже)");
        }

        private void OpenSettings()
        {
            Debug.Log("[Menu] Кнопка 'Настройки' нажата! (Окно настроек мы сделаем на следующем этапе)");
        }
    }
}
