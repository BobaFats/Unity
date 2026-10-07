using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Tanks2D
{
    // УСТАРЕЛО: заменено универсальным UpgradeButton (UpgradeType). Файл можно удалить.
    public class UpgradeFireRateButton : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private TextMeshProUGUI upgradeInfoText; 
        [SerializeField] private Button buyButton; 

        [Header("Upgrade Settings")]
        [SerializeField] private string upgradeName = "Скорострельность";
        [SerializeField] private int basePrice = 25; 
        [SerializeField] private float priceMultiplier = 1.6f;
        [SerializeField] private float fireRateDecrease = 0.04f; 
        [SerializeField] private float minFireRateLimit = 0.1f; 

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
            if (buyButton != null)
            {
                if (GameStats.FireDelay <= minFireRateLimit)
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

            if (GameStats.FireDelay <= minFireRateLimit) return;

            if (Wallet.TrySpendGold(cost))
            {
                _currentLevel++;
                GameStats.FireDelay = Mathf.Max(minFireRateLimit, GameStats.FireDelay - fireRateDecrease); 
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
                if (GameStats.FireDelay <= minFireRateLimit)
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
