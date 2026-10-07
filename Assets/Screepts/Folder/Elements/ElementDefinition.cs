using UnityEngine;

namespace Tanks2D
{
    // Данные одного попадания пули. Стихии могут менять урон (крит) до нанесения
    // и накладывать эффекты (горение, замедление, отталкивание) после.
    public struct HitContext
    {
        public PigEnemy Enemy;
        public int Damage;
        public bool IsCrit;
        public Color? TextColor;
        public Vector2 Direction;
    }

    // Базовый класс стихии пули. Чтобы добавить новую стихию:
    //   1) унаследуйте класс от ElementDefinition и переопределите ModifyHit/AfterHit/DescribeEffect;
    //   2) создайте ассет через Create > Shooter > Elements;
    //   3) добавьте его в Assets/Resources/ElementCatalog.asset.
    // После этого стихия сама появится в наградах за уровень и за босса.
    public abstract class ElementDefinition : ScriptableObject
    {
        [Header("Описание")]
        [Tooltip("Уникальный ключ для сохранения уровня. Пусто — используется имя ассета")]
        [SerializeField] private string _id;
        [SerializeField] private string _displayName = "Стихия";
        [SerializeField] private VisualId _icon = VisualId.ElementFire;
        [Tooltip("Цвет цифр урона и подсветки")]
        [SerializeField] private Color _color = Color.white;

        [Header("Шанс срабатывания")]
        [SerializeField, Min(1)] private int _maxLevel = 5;
        [SerializeField, Range(0f, 1f)] private float _baseChance = 0.2f;
        [SerializeField, Range(0f, 1f)] private float _chancePerLevel = 0.1f;
        [SerializeField, Range(0f, 1f)] private float _maxChance = 0.8f;

        [Tooltip("Порядок обработки: меньше — раньше (модификаторы урона должны идти первыми)")]
        [SerializeField] private int _order;

        public string Id => string.IsNullOrEmpty(_id) ? name : _id;
        public string DisplayName => _displayName;
        public VisualId Icon => _icon;
        public Color Color => _color;
        public int MaxLevel => _maxLevel;
        public int Order => _order;

        public int Level
        {
            get => GameStats.GetElementLevel(Id);
            set => GameStats.SetElementLevel(Id, Mathf.Clamp(value, 0, _maxLevel));
        }

        public bool IsMaxed => Level >= _maxLevel;

        public float ChanceAt(int level)
        {
            if (level <= 0) return 0f;
            return Mathf.Min(_maxChance, _baseChance + _chancePerLevel * (level - 1));
        }

        // Срабатывание до нанесения урона (например, крит)
        public virtual void ModifyHit(ref HitContext hit, int level) { }

        // Срабатывание после нанесения урона (эффекты на врага)
        public virtual void AfterHit(HitContext hit, int level) { }

        // Описание силы эффекта на уровне level (без шанса)
        public abstract string DescribeEffect(int level);

        public string DescribeLevel(int level)
        {
            return $"Шанс {ChanceAt(level) * 100f:0}%: {DescribeEffect(level)}";
        }

        protected static float Scale(float baseValue, float perLevel, int level)
        {
            return baseValue + perLevel * Mathf.Max(0, level - 1);
        }
    }
}
