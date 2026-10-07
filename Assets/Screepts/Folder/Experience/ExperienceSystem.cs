using System;
using System.Collections.Generic;
using UnityEngine;

namespace Tanks2D
{
    // Один вариант награды в окне выбора
    public class UpgradeChoice
    {
        public string Id;
        public string Title;
        public string Description;
        public VisualId Icon;
        public Action Apply;
    }

    // Опыт за убийства -> новый уровень -> пауза и выбор награды:
    // башня-помощник, урон, количество пуль, разброс, а после первого босса — стихии пуль.
    // Победа над боссом даёт отдельную награду — выбор стихии.
    // Прогресс хранится в GameStats и переживает переход между сценами.
    public class ExperienceSystem : MonoBehaviour
    {
        private enum OfferKind { LevelUp, BossReward }

        private struct Offer
        {
            public OfferKind Kind;
            public int Level;
            public Action OnDone;
        }

        [Header("Кривая опыта")]
        [Tooltip("Сколько опыта нужно для 2-го уровня")]
        [SerializeField] private int _baseXpToLevel = 5;
        [Tooltip("Во сколько раз растёт требование с каждым уровнем")]
        [SerializeField] private float _xpGrowth = 1.35f;

        [Header("Выбор награды")]
        [Tooltip("Сколько случайных вариантов показывать при новом уровне")]
        [SerializeField] private int _choicesPerLevel = 4;

        [Header("References")]
        [SerializeField] private LevelUpPanel _panel;
        [SerializeField] private TowerManager _towers;

        private readonly Queue<Offer> _offers = new Queue<Offer>();
        private bool _showing;

        public static ExperienceSystem Instance { get; private set; }

        public int XpToNextLevel => XpRequiredFor(GameStats.Level);
        public float Progress => Mathf.Clamp01((float)GameStats.Experience / Mathf.Max(1, XpToNextLevel));
        public bool IsChoosing => _showing;
        public bool IsShowingBossReward => _showing && _offers.Count > 0 && _offers.Peek().Kind == OfferKind.BossReward;

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
                _offers.Enqueue(new Offer { Kind = OfferKind.LevelUp, Level = GameStats.Level });
            }

            ShowNext();
        }

        // Награда за босса: выбор стихии. onDone вызывается после выбора (например, переход в лобби).
        public void OfferBossReward(Action onDone)
        {
            _offers.Enqueue(new Offer { Kind = OfferKind.BossReward, OnDone = onDone });
            ShowNext();
        }

        private void ShowNext()
        {
            if (_showing || _panel == null || Wall.IsGameOver) return;

            while (_offers.Count > 0)
            {
                Offer offer = _offers.Peek();
                List<UpgradeChoice> choices = offer.Kind == OfferKind.BossReward ? BuildBossRewardChoices() : BuildLevelUpChoices();

                if (choices.Count == 0)
                {
                    _offers.Dequeue();
                    offer.OnDone?.Invoke();
                    continue;
                }

                string title = offer.Kind == OfferKind.BossReward
                    ? "БОСС ПОВЕРЖЕН!\n<size=60%>Выберите стихию пуль</size>"
                    : $"НОВЫЙ УРОВЕНЬ {offer.Level}!\n<size=60%>Выберите награду</size>";

                _showing = true;
                GamePause.Set(this, true);
                _panel.Show(title, choices, OnChosen);
                return;
            }

            _panel.Hide();
            GamePause.Set(this, false);
        }

        private void OnChosen(UpgradeChoice choice)
        {
            choice.Apply?.Invoke();

            Offer offer = _offers.Dequeue();
            _showing = false;

            if (_offers.Count == 0)
            {
                _panel.Hide();
                GamePause.Set(this, false);
            }

            offer.OnDone?.Invoke();
            ShowNext();
        }

        // ---------------------------------------------------------------- Варианты наград

        private List<UpgradeChoice> BuildLevelUpChoices()
        {
            var all = new List<UpgradeChoice>();
            AddWeaponChoices(all);
            if (GameStats.BossesDefeated > 0) AddElementChoices(all);

            // Случайные варианты из доступных
            for (int i = all.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (all[i], all[j]) = (all[j], all[i]);
            }

            if (all.Count > _choicesPerLevel) all.RemoveRange(_choicesPerLevel, all.Count - _choicesPerLevel);
            return all;
        }

        private List<UpgradeChoice> BuildBossRewardChoices()
        {
            var choices = new List<UpgradeChoice>();
            AddElementChoices(choices);
            if (choices.Count == 0) AddWeaponChoices(choices);
            return choices;
        }

        private void AddWeaponChoices(List<UpgradeChoice> list)
        {
            if (_towers != null && _towers.CanAddTower)
            {
                list.Add(new UpgradeChoice
                {
                    Id = "tower",
                    Title = "Башня-помощник",
                    Description = $"Стреляет сама ({GameStats.TowerCount} / {GameStats.MaxTowers})",
                    Icon = VisualId.IconTower,
                    Apply = () => _towers.AddTower()
                });
            }

            list.Add(new UpgradeChoice
            {
                Id = "damage",
                Title = $"Урон +{GameStats.XpDamageStep}",
                Description = $"{GameStats.BulletDamage} → {GameStats.BulletDamage + GameStats.XpDamageStep}",
                Icon = VisualId.IconDamage,
                Apply = () => GameStats.BulletDamage += GameStats.XpDamageStep
            });

            if (GameStats.WallMaxHP < GameStats.MaxWallHP)
            {
                int nextHP = Mathf.Min(GameStats.MaxWallHP, GameStats.WallMaxHP + GameStats.XpWallHPStep);
                list.Add(new UpgradeChoice
                {
                    Id = "wall",
                    Title = $"Стена +{GameStats.XpWallHPStep} HP",
                    Description = $"{GameStats.WallMaxHP} → {nextHP} HP и полный ремонт",
                    Icon = VisualId.IconWallHP,
                    Apply = () =>
                    {
                        GameStats.WallMaxHP = nextHP;
                        if (Wall.ActiveInstance != null) Wall.ActiveInstance.UpgradeAndRepair();
                    }
                });
            }

            if (GameStats.BulletCount < GameStats.MaxBulletCount)
            {
                list.Add(new UpgradeChoice
                {
                    Id = "multishot",
                    Title = "Пули +1",
                    Description = $"Залп: {GameStats.BulletCount} → {GameStats.BulletCount + 1}",
                    Icon = VisualId.IconMultiShot,
                    Apply = () => GameStats.BulletCount = Mathf.Min(GameStats.MaxBulletCount, GameStats.BulletCount + 1)
                });
            }

            if (GameStats.BulletCount > 1 && GameStats.SpreadAngle < GameStats.MaxSpreadAngle)
            {
                float next = Mathf.Min(GameStats.MaxSpreadAngle, GameStats.SpreadAngle + GameStats.SpreadStep);
                list.Add(new UpgradeChoice
                {
                    Id = "spread",
                    Title = "Шире разброс",
                    Description = $"{GameStats.SpreadAngle:0}° → {next:0}°",
                    Icon = VisualId.IconSpread,
                    Apply = () => GameStats.SpreadAngle = next
                });
            }
        }

        private static void AddElementChoices(List<UpgradeChoice> list)
        {
            ElementCatalog catalog = ElementCatalog.Instance;
            if (catalog == null) return;

            foreach (ElementDefinition element in catalog.Elements)
            {
                if (element == null || element.IsMaxed) continue;

                int level = element.Level;
                ElementDefinition captured = element;

                list.Add(new UpgradeChoice
                {
                    Id = "element:" + element.Id,
                    Title = level == 0 ? $"{element.DisplayName} (новая стихия)" : $"{element.DisplayName} ур. {level + 1}",
                    Description = element.DescribeLevel(level + 1),
                    Icon = element.Icon,
                    Apply = () => captured.Level = captured.Level + 1
                });
            }
        }
    }
}
