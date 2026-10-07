using UnityEngine;
using TMPro;

namespace Tanks2D
{
    public class StatsDisplay : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private TextMeshProUGUI statsText;

        private void Update()
        {
            if (statsText == null) return;

            statsText.text =
                $"<color=#FF6A3D>Урон:</color> {GameStats.BulletDamage}\n" +
                $"<color=#3DC8FF>Задержка выстрела:</color> {GameStats.FireDelay:F2} сек.\n" +
                $"<color=#FFD23D>Перезарядка:</color> {GameStats.ReloadTime:F1} сек.\n" +
                $"<color=#5BE06A>Обойма:</color> {GameStats.MaxAmmo} патр.\n" +
                $"<color=#C8A07A>Стена:</color> {GameStats.WallMaxHP} HP\n" +
                $"<color=#B98CFF>Пуль в залпе:</color> {GameStats.BulletCount}   <color=#B98CFF>Разброс:</color> {GameStats.SpreadAngle:0}°\n" +
                $"<color=#8CE0FF>Башни:</color> {GameStats.TowerCount} / {GameStats.MaxTowers}   <color=#8CE0FF>Уровень:</color> {GameStats.Level}";
        }
    }
}
