using System.Collections.Generic;
using UnityEngine;

namespace Tanks2D
{
    // Обработка попадания пули: каждая изученная стихия бросает свой шанс независимо,
    // поэтому на одном попадании могут сработать сразу несколько (эффекты складываются).
    public static class ElementSystem
    {
        private static readonly List<(ElementDefinition element, int level)> _triggered = new List<(ElementDefinition, int)>();

        public static bool AnyUnlocked
        {
            get
            {
                ElementCatalog catalog = ElementCatalog.Instance;
                if (catalog == null) return false;
                foreach (ElementDefinition element in catalog.Elements)
                {
                    if (element != null && element.Level > 0) return true;
                }
                return false;
            }
        }

        public static void ResolveHit(PigEnemy enemy, int damage, Vector2 direction)
        {
            if (enemy == null || enemy.IsDying) return;

            var hit = new HitContext { Enemy = enemy, Damage = damage, Direction = direction };
            _triggered.Clear();

            ElementCatalog catalog = ElementCatalog.Instance;
            if (catalog != null)
            {
                foreach (ElementDefinition element in catalog.Sorted)
                {
                    int level = element.Level;
                    if (level <= 0 || Random.value >= element.ChanceAt(level)) continue;

                    element.ModifyHit(ref hit, level);
                    _triggered.Add((element, level));
                }
            }

            enemy.ApplyDamage(hit.Damage, hit.TextColor, hit.IsCrit);
            if (enemy.IsDying) return;

            foreach ((ElementDefinition element, int level) in _triggered)
            {
                element.AfterHit(hit, level);
            }
        }
    }
}
