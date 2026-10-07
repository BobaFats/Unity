using UnityEngine;

namespace Tanks2D
{
    // Земля: критический удар — урон попадания умножается
    [CreateAssetMenu(fileName = "Earth", menuName = "Shooter/Elements/Earth")]
    public class EarthElement : ElementDefinition
    {
        [Header("Критический урон")]
        [SerializeField] private float _critMultiplier = 2f;
        [SerializeField] private float _critMultiplierPerLevel = 0.25f;

        public override void ModifyHit(ref HitContext hit, int level)
        {
            hit.Damage = Mathf.RoundToInt(hit.Damage * Scale(_critMultiplier, _critMultiplierPerLevel, level));
            hit.IsCrit = true;
            hit.TextColor = Color;
        }

        public override string DescribeEffect(int level)
        {
            return $"крит x{Scale(_critMultiplier, _critMultiplierPerLevel, level):0.##}";
        }
    }
}
