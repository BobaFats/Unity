using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Tanks2D
{
    // УСТАРЕЛО: заменено универсальным UpgradeButton (UpgradeType). Файл можно удалить.
    public class UpgradeWallHPButton : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private TextMeshProUGUI upgradeInfoText; 
        [SerializeField] private Button buyButton; 

        [Header("Upgrade Settings")]
        [SerializeField] private string upgradeName = "Прочность стены";
        [SerializeField] private int basePrice = 20; 
        [SerializeField] private float priceMultiplier = 1.6f;
        [SerializeField] private int hpIncreasePerLevel = 50; // Сколько здоровья добавляем за уровень (+50 HP)
        [SerializeField] private int maxHPLimit = 500; // Лимит прокачки здоровья стены

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
            // Управляем доступностью кнопки серая/активная (проверяем золото и лимит)
            if (buyButton != null)
            {
                if (GameStats.WallMaxHP >= maxHPLimit)
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

            if (GameStats.WallMaxHP >= maxHPLimit) return;

            // Обращаемся к глобальному кошельку (он лежит вне namespace Tanks2D, поэтому пишется без точек)
            if (global::Wallet.TrySpendGold(cost))
            {
                _currentLevel++;
                
                // Увеличиваем максимальное здоровье в переменной стены
                GameStats.WallMaxHP = Mathf.Min(maxHPLimit, GameStats.WallMaxHP + hpIncreasePerLevel); 
                
                // Просим живую стену на сцене применить изменения и мгновенно починиться
                if (Wall.ActiveInstance != null)
                {
                    Wall.ActiveInstance.UpgradeAndRepair();
                }

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
                if (GameStats.WallMaxHP >= maxHPLimit)
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
