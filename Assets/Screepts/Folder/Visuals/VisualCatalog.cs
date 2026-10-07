using System;
using System.Collections.Generic;
using UnityEngine;

namespace Tanks2D
{
    // Идентификаторы всех визуальных элементов игры.
    // Чтобы заменить заглушку на арт — назначьте Sprite (и при желании Animator Controller)
    // нужной записи в Assets/Resources/VisualCatalog.asset. Код и префабы трогать не нужно.
    public enum VisualId
    {
        Background,
        Wall,
        Player,
        Weapon,
        Bullet,
        EnemyPig,
        EnemyChicken,
        Boss,
        Coin,
        IconDamage,
        IconFireRate,
        IconReload,
        IconAmmo,
        IconWallHP,
        // Новые значения добавлять только в конец: каталог хранит их числами
        Tower,
        IconTower,
        IconMultiShot,
        IconSpread,
        BossShield,
        ElementFire,
        ElementIce,
        ElementWind,
        ElementEarth
    }

    public enum PlaceholderShape
    {
        Rectangle,
        Circle,
        Triangle,
        Diamond
    }

    [Serializable]
    public class VisualEntry
    {
        public VisualId id;

        [Tooltip("Финальный арт. Пока пусто — рисуется заглушка (фигура + цвет + подпись).")]
        public Sprite sprite;

        [Tooltip("Необязательно: аниматор для финального арта (триггеры Attack / Die у врагов).")]
        public RuntimeAnimatorController animator;

        [Header("Заглушка")]
        public PlaceholderShape shape = PlaceholderShape.Rectangle;
        public Color color = Color.white;
        public string label;

        [Tooltip("Размер объекта в мировых единицах (для UI — относительный, не используется). Арт вписывается в этот размер с сохранением пропорций.")]
        public Vector2 size = Vector2.one;

        public bool HasArt => sprite != null;

        public static VisualEntry Fallback(VisualId id)
        {
            return new VisualEntry { id = id, shape = PlaceholderShape.Rectangle, color = Color.magenta, label = id.ToString() };
        }
    }

    [CreateAssetMenu(fileName = "VisualCatalog", menuName = "Shooter/Visual Catalog")]
    public class VisualCatalog : ScriptableObject
    {
        public const string ResourcePath = "VisualCatalog";

        [SerializeField] private List<VisualEntry> _entries = new List<VisualEntry>();

        private static VisualCatalog _instance;

        public static VisualCatalog Instance
        {
            get
            {
                if (_instance == null) _instance = Resources.Load<VisualCatalog>(ResourcePath);
                return _instance;
            }
        }

        public IReadOnlyList<VisualEntry> Entries => _entries;

        // Для редакторных инструментов: только что созданный ассет может ещё не находиться через Resources.Load
        public static void SetInstance(VisualCatalog catalog)
        {
            _instance = catalog;
        }

        public static VisualEntry Resolve(VisualId id)
        {
            VisualCatalog catalog = Instance;
            if (catalog != null && catalog.TryGet(id, out VisualEntry entry)) return entry;
            return VisualEntry.Fallback(id);
        }

        public bool TryGet(VisualId id, out VisualEntry entry)
        {
            foreach (VisualEntry e in _entries)
            {
                if (e.id == id)
                {
                    entry = e;
                    return true;
                }
            }

            entry = null;
            return false;
        }

        // Добавляет запись только если её ещё нет — правки дизайнера не затираются
        public bool AddIfMissing(VisualEntry entry)
        {
            if (TryGet(entry.id, out _)) return false;
            _entries.Add(entry);
            return true;
        }
    }
}
