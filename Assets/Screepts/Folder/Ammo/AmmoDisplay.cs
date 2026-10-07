using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Tanks2D
{
    public class AmmoDisplay : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private TextMeshProUGUI ammoText;
        [SerializeField] private Slider reloadSlider;
        [Tooltip("Кнопка перезарядки для телефона (необязательно)")]
        [SerializeField] private Button reloadButton;

        [Header("References")]
        [SerializeField] private PlayerController2D playerController;

        private void Start()
        {
            if (reloadSlider != null) reloadSlider.gameObject.SetActive(false);
            if (reloadButton != null && playerController != null) reloadButton.onClick.AddListener(playerController.RequestReload);
        }

        private void Update()
        {
            if (playerController == null) return;

            bool reloading = playerController.IsReloading;

            if (ammoText != null)
            {
                ammoText.text = reloading
                    ? "<color=#FF5050>ПЕРЕЗАРЯДКА...</color>"
                    : $"Патроны: {playerController.CurrentAmmo} / {GameStats.MaxAmmo}";
            }

            if (reloadSlider != null)
            {
                if (reloadSlider.gameObject.activeSelf != reloading) reloadSlider.gameObject.SetActive(reloading);
                if (reloading) reloadSlider.value = playerController.ReloadProgress;
            }

            if (reloadButton != null)
            {
                reloadButton.interactable = !reloading && playerController.CurrentAmmo < GameStats.MaxAmmo;
            }
        }
    }
}
