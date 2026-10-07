using TMPro;
using UnityEngine;

namespace Tanks2D
{
    // Показывает в HUD, сколько врагов осталось до появления босса
    public class WaveProgressUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _text;

        private void Update()
        {
            EnemySpawner2D spawner = EnemySpawner2D.Instance;
            if (_text == null || spawner == null) return;

            if (spawner.IsBossDefeated) _text.text = "<color=#5BE06A>ПОБЕДА!</color>";
            else if (spawner.IsBossSpawned) _text.text = "<color=#FF5050>БОСС!</color>";
            else _text.text = $"До босса: {spawner.KillsCount} / {spawner.KillsNeededForBoss}";

            int minute = Mathf.FloorToInt(spawner.BattleTime / 60f);
            int seconds = Mathf.FloorToInt(spawner.BattleTime % 60f);
            _text.text += $"\n<size=75%>{minute}:{seconds:00}  ·  враги x{spawner.SpawnMultiplier}</size>";
        }
    }
}
