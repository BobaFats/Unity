using TMPro;
using UnityEngine;

namespace Tanks2D
{
    // Лобби между уровнями: номер и название следующего уровня, способности его босса
    public class LobbyUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _missionText;

        private void Start()
        {
            GamePause.Clear();
        }

        private void Update()
        {
            if (_missionText == null) return;

            int number = GameStats.Mission;
            LevelDefinition level = LevelCatalog.Current;

            string title = level != null ? level.DisplayName : $"Уровень {number}";
            BossAbility abilities = level != null
                ? level.bossAbilities
                : number <= 1 ? BossAbility.Shield : BossAbility.Shield | BossAbility.RamWall | BossAbility.DestroyTowers;

            string details = $"Босс: {LevelDefinition.DescribeAbilities(abilities)}";
            if (level != null && level.miniBosses.Count > 0) details += $"\nМини-боссов: {level.miniBosses.Count}";
            if (GameStats.CampaignCompleted) details = "<color=#5BE06A>Кампания пройдена!</color> Можно перепройти последний уровень";

            _missionText.text = $"{title}  <size=60%>({number} / {GameStats.TotalLevels})</size>\n<size=55%>{details}</size>";
        }
    }
}
