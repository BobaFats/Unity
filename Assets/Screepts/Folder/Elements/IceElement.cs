using UnityEngine;

namespace Tanks2D
{
    // Холод: замедляет движение врага
    [CreateAssetMenu(fileName = "Ice", menuName = "Shooter/Elements/Ice")]
    public class IceElement : ElementDefinition
    {
        [Header("Замедление")]
        [SerializeField, Range(0f, 1f)] private float _slow = 0.3f;
        [SerializeField, Range(0f, 1f)] private float _slowPerLevel = 0.08f;
        [SerializeField, Range(0f, 1f)] private float _maxSlow = 0.75f;
        [SerializeField] private float _duration = 2f;

        private float SlowAt(int level) => Mathf.Min(_maxSlow, Scale(_slow, _slowPerLevel, level));

        public override void AfterHit(HitContext hit, int level)
        {
            hit.Enemy.ApplySlow(SlowAt(level), _duration);
        }

        public override string DescribeEffect(int level)
        {
            return $"замедление на {SlowAt(level) * 100f:0}%, {_duration:0} сек.";
        }
    }
}
