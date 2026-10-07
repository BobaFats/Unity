using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Tanks2D
{
    // Одна кнопка улучшения любой характеристики (заменяет пять почти одинаковых скриптов).
    // Значение и уровень хранятся в GameStats, поэтому переживают переход между сценами.
    public class UpgradeButton : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private TextMeshProUGUI upgradeInfoText;
        [SerializeField] private Button buyButton;

        [Header("Upgrade Settings")]
        [SerializeField] private UpgradeType upgradeType = UpgradeType.Damage;
        [SerializeField] private string upgradeName = "Урон пули";
        [SerializeField] private int basePrice = 10;
        [SerializeField] private float priceMultiplier = 1.5f;
        [Tooltip("Изменение за уровень. Отрицательное — характеристика уменьшается (задержка, время перезарядки)")]
        [SerializeField] private float stepPerLevel = 1f;
        [Tooltip("Предел прокачки (минимум для отрицательного шага, максимум для положительного). 0 — без предела")]
        [SerializeField] private float limit = 0f;

        private bool HasLimit => !Mathf.Approximately(limit, 0f);

        private bool IsMaxed
        {
            get
            {
                if (!HasLimit) return false;
                float current = GameStats.GetValue(upgradeType);
                return stepPerLevel < 0f ? current <= limit + 0.0001f : current >= limit - 0.0001f;
            }
        }

        private int Price => Mathf.RoundToInt(basePrice * Mathf.Pow(priceMultiplier, GameStats.GetLevel(upgradeType) - 1));

        private void Start()
        {
            if (buyButton != null) buyButton.onClick.AddListener(BuyUpgrade);
        }

        private void Update()
        {
            if (buyButton != null) buyButton.interactable = !IsMaxed && Wallet.TotalGold >= Price;
            UpdateUI();
        }

        public void BuyUpgrade()
        {
            if (IsMaxed || !Wallet.TrySpendGold(Price)) return;

            float next = GameStats.GetValue(upgradeType) + stepPerLevel;
            if (HasLimit) next = stepPerLevel < 0f ? Mathf.Max(limit, next) : Mathf.Min(limit, next);

            GameStats.SetValue(upgradeType, next);
            GameStats.IncrementLevel(upgradeType);

            if (upgradeType == UpgradeType.WallHP && Wall.ActiveInstance != null)
            {
                Wall.ActiveInstance.UpgradeAndRepair();
            }

            UpdateUI();
        }

        private void UpdateUI()
        {
            if (upgradeInfoText == null) return;

            string price = IsMaxed ? "МАКС." : $"Цена: {Price} зл.";
            upgradeInfoText.text = $"{upgradeName} (LVL {GameStats.GetLevel(upgradeType)})\n{price}";
        }
    }
}
