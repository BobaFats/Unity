using UnityEngine;

namespace Tanks2D
{
    public enum LevelUpOption
    {
        Tower,
        Damage,
        MultiShot,
        Spread
    }

    // Опыт за убийства -> новый уровень -> игра на паузе, игрок выбирает одну награду:
    // построить башню-помощника или улучшить оружие (урон / количество пуль / разброс).
    // Опыт, уровень и выбранные улучшения хранятся в GameStats и переживают переход между сценами.
    public class ExperienceSystem : MonoBehaviour
    {
        [Header("Кривая опыта")]
        [Tooltip("Сколько опыта нужно для 2-го уровня")]
        [SerializeField] private int _baseXpToLevel = 5;
        [Tooltip("Во сколько раз растёт требование с каждым уровнем")]
        [SerializeField] private float _xpGrowth = 1.35f;

        [Header("References")]
        [SerializeField] private LevelUpPanel _panel;
        [SerializeField] private TowerManager _towers;

        private int _pendingLevelUps;

        public static ExperienceSystem Instance { get; private set; }

        public int XpToNextLevel => XpRequiredFor(GameStats.Level);
        public float Progress => Mathf.Clamp01((float)GameStats.Experience / Mathf.Max(1, XpToNextLevel));

        // OnEnable, а не Awake: переживает перезагрузку скриптов прямо в Play Mode
        private void OnEnable()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            GamePause.Set(this, false);
            if (Instance == this) Instance = null;
        }

        public static void AddExperience(int amount)
        {
            if (amount <= 0) return;

            GameStats.Experience += amount;
            if (Instance != null) Instance.CheckLevelUp();
        }

        public int XpRequiredFor(int level)
        {
            return Mathf.Max(1, Mathf.RoundToInt(_baseXpToLevel * Mathf.Pow(_xpGrowth, level - 1)));
        }

        private void CheckLevelUp()
        {
            while (GameStats.Experience >= XpToNextLevel)
            {
                GameStats.Experience -= XpToNextLevel;
                GameStats.Level++;
                _pendingLevelUps++;
            }

            if (_pendingLevelUps > 0 && _panel != null && !_panel.IsOpen && !Wall.IsGameOver)
            {
                ShowChoice();
            }
        }

        private void ShowChoice()
        {
            GamePause.Set(this, true);
            _panel.Show(GameStats.Level - _pendingLevelUps + 1, IsAvailable, Describe, OnOptionChosen);
        }

        private void OnOptionChosen(LevelUpOption option)
        {
            Apply(option);
            _pendingLevelUps = Mathf.Max(0, _pendingLevelUps - 1);

            if (_pendingLevelUps > 0)
            {
                ShowChoice();
                return;
            }

            _panel.Hide();
            GamePause.Set(this, false);
        }

        public bool IsAvailable(LevelUpOption option)
        {
            switch (option)
            {
                case LevelUpOption.Tower: return _towers != null && _towers.CanAddTower;
                case LevelUpOption.Damage: return true;
                case LevelUpOption.MultiShot: return GameStats.BulletCount < GameStats.MaxBulletCount;
                case LevelUpOption.Spread: return GameStats.BulletCount > 1 && GameStats.SpreadAngle < GameStats.MaxSpreadAngle;
                default: return false;
            }
        }

        public string Describe(LevelUpOption option)
        {
            switch (option)
            {
                case LevelUpOption.Tower:
                    return $"<b>Башня-помощник</b>\nСтреляет сама ({GameStats.TowerCount} / {GameStats.MaxTowers})";
                case LevelUpOption.Damage:
                    return $"<b>Урон +{GameStats.XpDamageStep}</b>\n{GameStats.BulletDamage} → {GameStats.BulletDamage + GameStats.XpDamageStep}";
                case LevelUpOption.MultiShot:
                    return $"<b>Пули +1</b>\nЗалп: {GameStats.BulletCount} → {GameStats.BulletCount + 1}";
                case LevelUpOption.Spread:
                    float next = Mathf.Min(GameStats.MaxSpreadAngle, GameStats.SpreadAngle + GameStats.SpreadStep);
                    return $"<b>Шире разброс</b>\n{GameStats.SpreadAngle:0}° → {next:0}°";
                default:
                    return option.ToString();
            }
        }

        private void Apply(LevelUpOption option)
        {
            switch (option)
            {
                case LevelUpOption.Tower:
                    if (_towers != null) _towers.AddTower();
                    break;
                case LevelUpOption.Damage:
                    GameStats.BulletDamage += GameStats.XpDamageStep;
                    break;
                case LevelUpOption.MultiShot:
                    GameStats.BulletCount = Mathf.Min(GameStats.MaxBulletCount, GameStats.BulletCount + 1);
                    break;
                case LevelUpOption.Spread:
                    GameStats.SpreadAngle = Mathf.Min(GameStats.MaxSpreadAngle, GameStats.SpreadAngle + GameStats.SpreadStep);
                    break;
            }
        }
    }
}
