using UnityEngine;
using UnityEngine.UI;

namespace Tanks2D
{
    public class ShopController : MonoBehaviour
    {
        [Header("Shop UI Panel")]
        [SerializeField] private GameObject shopPanel;

        [Header("Control Buttons")]
        [SerializeField] private Button openButton;
        [SerializeField] private Button closeButton;

        // Оставлено для совместимости со старым кодом; общая пауза — GamePause.IsPaused
        public static bool IsPaused => GamePause.IsPaused;

        private void Start()
        {
            if (shopPanel != null) shopPanel.SetActive(false);
            if (openButton != null) openButton.gameObject.SetActive(true);

            if (openButton != null) openButton.onClick.AddListener(OpenShop);
            if (closeButton != null) closeButton.onClick.AddListener(CloseShop);
        }

        public void OpenShop()
        {
            if (shopPanel == null || Wall.IsGameOver) return;

            shopPanel.SetActive(true);
            if (openButton != null) openButton.gameObject.SetActive(false);
            GamePause.Set(this, true);
        }

        public void CloseShop()
        {
            if (shopPanel == null) return;

            shopPanel.SetActive(false);
            if (openButton != null) openButton.gameObject.SetActive(true);
            GamePause.Set(this, false);
        }

        private void OnDestroy()
        {
            GamePause.Set(this, false);
        }
    }
}
