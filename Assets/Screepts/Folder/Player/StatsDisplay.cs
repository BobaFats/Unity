using System.Text;
using UnityEngine;
using TMPro;

namespace Tanks2D
{
    // Сводка прогресса: оружие, башни, стихии (лобби и меню паузы)
    public class StatsDisplay : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private TextMeshProUGUI statsText;

        private readonly StringBuilder _builder = new StringBuilder();

        private void Update()
        {
            if (statsText == null) return;

            _builder.Clear();
            _builder.AppendLine($"<color=#8CE0FF>Уровень:</color> {GameStats.Level}   <color=#8CE0FF>Боссов побеждено:</color> {GameStats.BossesDefeated}");
            _builder.AppendLine($"<color=#FF6A3D>Урон:</color> {GameStats.BulletDamage}   <color=#B98CFF>Пуль в залпе:</color> {GameStats.BulletCount}   <color=#B98CFF>Разброс:</color> {GameStats.SpreadAngle:0}°");
            _builder.AppendLine($"<color=#3DC8FF>Задержка выстрела:</color> {GameStats.FireDelay:F2} сек.   <color=#5BE06A>Обойма:</color> {GameStats.MaxAmmo}");
            _builder.AppendLine($"<color=#C8A07A>Стена:</color> {GameStats.WallMaxHP} HP   <color=#8CE0FF>Башни:</color> {GameStats.TowerCount} / {GameStats.MaxTowers}");

            ElementCatalog catalog = ElementCatalog.Instance;
            if (catalog != null)
            {
                bool any = false;
                foreach (ElementDefinition element in catalog.Elements)
                {
                    if (element == null || element.Level <= 0) continue;
                    if (!any) _builder.AppendLine("<color=#FFD23D>Стихии:</color>");
                    any = true;
                    string hex = ColorUtility.ToHtmlStringRGB(element.Color);
                    _builder.AppendLine($"  <color=#{hex}>{element.DisplayName} ур. {element.Level}</color> — {element.DescribeLevel(element.Level)}");
                }

                if (!any && GameStats.BossesDefeated == 0) _builder.AppendLine("<color=#888888>Стихии откроются после первого босса</color>");
            }

            statsText.text = _builder.ToString();
        }
    }
}
