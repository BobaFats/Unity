using UnityEngine;

namespace Tanks2D
{
    // Огонь: поджигает врага — урон каждую секунду в течение нескольких секунд
    [CreateAssetMenu(fileName = "Fire", menuName = "Shooter/Elements/Fire")]
    public class FireElement : ElementDefinition
    {
        [Header("Горение")]
        [Tooltip("Урон в секунду в долях от урона попадания")]
        [SerializeField] private float _damagePerSecond = 0.3f;
        [SerializeField] private float _damagePerSecondPerLevel = 0.1f;
        [SerializeField] private float _duration = 3f;

        public override void AfterHit(HitContext hit, int level)
        {
            float dps = Mathf.Max(1f, hit.Damage * Scale(_damagePerSecond, _damagePerSecondPerLevel, level));
            hit.Enemy.ApplyBurn(dps, _duration, Color);
        }

        public override string DescribeEffect(int level)
        {
            return $"горение {Scale(_damagePerSecond, _damagePerSecondPerLevel, level) * 100f:0}% урона/сек, {_duration:0} сек.";
        }
    }
}
