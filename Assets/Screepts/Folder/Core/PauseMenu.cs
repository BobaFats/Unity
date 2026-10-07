using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Tanks2D
{
    // Пауза: кнопка в HUD или Esc / P. В панели — продолжить, лобби, главное меню (последние две — SceneLoadButton).
    public class PauseMenu : MonoBehaviour
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private Button _pauseButton;
        [SerializeField] private Button _resumeButton;

        public bool IsOpen => _panel != null && _panel.activeSelf;

        private void Start()
        {
            if (_panel != null) _panel.SetActive(false);
            if (_pauseButton != null) _pauseButton.onClick.AddListener(Open);
            if (_resumeButton != null) _resumeButton.onClick.AddListener(Close);
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.escapeKey.wasPressedThisFrame || keyboard.pKey.wasPressedThisFrame) Toggle();
        }

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        public void Open()
        {
            if (_panel == null || Wall.IsGameOver) return;

            _panel.SetActive(true);
            if (_pauseButton != null) _pauseButton.gameObject.SetActive(false);
            GamePause.Set(this, true);
        }

        public void Close()
        {
            if (_panel == null) return;

            _panel.SetActive(false);
            if (_pauseButton != null) _pauseButton.gameObject.SetActive(true);
            GamePause.Set(this, false);
        }

        private void OnDestroy()
        {
            GamePause.Set(this, false);
        }
    }
}
