using TMPro;
using UnityEngine;

namespace Tanks2D
{
    // Лобби между миссиями: номер следующей миссии и чего ждать от босса
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

            int mission = GameStats.Mission;
            string boss = mission >= 2
                ? "Босс: щит + таран с откатом"
                : "Босс: включает щит";

            _missionText.text = $"Миссия {mission}\n<size=60%>{boss}</size>";
        }
    }
}
