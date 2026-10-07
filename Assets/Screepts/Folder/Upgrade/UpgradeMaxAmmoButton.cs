using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Tanks2D
{
    // УСТАРЕЛО: заменено универсальным UpgradeButton (UpgradeType). Файл можно удалить.
    public class UpgradeMaxAmmoButton : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private TextMeshProUGUI upgradeInfoText; 
        [SerializeField] private Button buyButton; 

        [Header("Upgrade Settings")]
        [SerializeField] private string upgradeName = "Размер обоймы";
        [SerializeField] private int basePrice = 30; 
        [SerializeField] private float priceMultiplier = 1.7f;
        [SerializeField] private int ammoIncreasePerLevel = 5; 
        [SerializeField] private int maxAmmoLimit = 50; 

        private int _currentLevel = 1;

        private void Start()
        {
            if (buyButton != null)
            {
                buyButton.onClick.AddListener(BuyUpgrade);
            }
            UpdateUI();
        }

        private void Update()
        {
            // НОВОЕ: Управляем доступностью кнопки
            if (buyButton != null)
            {
                // Если обойма уже максимального размера
                if (GameStats.MaxAmmo >= maxAmmoLimit)
                {
                    buyButton.interactable = false;
                    return;
                }

                int cost = CalculatePrice();
                buyButton.interactable = (Wallet.TotalGold >= cost);
            }
        }

        public void BuyUpgrade()
        {
            int cost = CalculatePrice();

            if (GameStats.MaxAmmo >= maxAmmoLimit) return;

            if (Wallet.TrySpendGold(cost))
            {
                _currentLevel++;
                GameStats.MaxAmmo = Mathf.Min(maxAmmoLimit, GameStats.MaxAmmo + ammoIncreasePerLevel); 
                UpdateUI();
            }
        }

        private int CalculatePrice()
        {
            return Mathf.RoundToInt(basePrice * Mathf.Pow(priceMultiplier, _currentLevel - 1));
        }

        private void UpdateUI()
        {
            if (upgradeInfoText != null)
            {
                if (GameStats.MaxAmmo >= maxAmmoLimit)
                {
                    upgradeInfoText.text = $"{upgradeName} (LVL {_currentLevel})\nМАКС.";
                }
                else
                {
                    upgradeInfoText.text = $"{upgradeName} (LVL {_currentLevel})\nЦена: {CalculatePrice()} зл.";
                }
            }
        }
    }
}
