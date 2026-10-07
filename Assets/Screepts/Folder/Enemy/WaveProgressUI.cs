using TMPro;
using UnityEngine;

namespace Tanks2D
{
    // HUD: номер миссии, прогресс до босса, время боя и множитель потока врагов
    public class WaveProgressUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _text;
        [Tooltip("Необязательно: отдельная надпись с номером миссии")]
        [SerializeField] private TextMeshProUGUI _missionText;

        private void Update()
        {
            if (_missionText != null) _missionText.text = $"Миссия {GameStats.Mission}";

            EnemySpawner2D spawner = EnemySpawner2D.Instance;
            if (_text == null || spawner == null) return;

            if (spawner.IsBossDefeated) _text.text = "<color=#5BE06A>ПОБЕДА!</color>";
            else if (spawner.IsBossSpawned) _text.text = $"<color=#FF5050>БОСС ур. {spawner.BossLevel}!</color>";
            else _text.text = $"До босса: {spawner.KillsCount} / {spawner.KillsNeededForBoss}";

            int minute = Mathf.FloorToInt(spawner.BattleTime / 60f);
            int seconds = Mathf.FloorToInt(spawner.BattleTime % 60f);
            _text.text += $"\n<size=75%>{minute}:{seconds:00}  ·  враги x{spawner.SpawnMultiplier}  ·  HP x{spawner.HealthMultiplier:0.0}</size>";
        }
    }
}
