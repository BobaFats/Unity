using UnityEngine;

namespace Tanks2D
{
    // Ветер: отбрасывает врага назад (вверх, от стены)
    [CreateAssetMenu(fileName = "Wind", menuName = "Shooter/Elements/Wind")]
    public class WindElement : ElementDefinition
    {
        [Header("Отталкивание")]
        [Tooltip("Дистанция отбрасывания в мировых единицах")]
        [SerializeField] private float _distance = 0.8f;
        [SerializeField] private float _distancePerLevel = 0.25f;

        public override void AfterHit(HitContext hit, int level)
        {
            hit.Enemy.Knockback(Scale(_distance, _distancePerLevel, level));
        }

        public override string DescribeEffect(int level)
        {
            return $"отбрасывает на {Scale(_distance, _distancePerLevel, level):0.0} м";
        }
    }
}
