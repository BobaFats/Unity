using UnityEngine;
using UnityEngine.SceneManagement;

namespace Tanks2D
{
    // Время забега для итогов: идёт везде, кроме главного меню и экрана итогов (и не идёт на паузе).
    // Создаётся сам при запуске игры и живёт между сценами.
    public class RunClock : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (FindAnyObjectByType<RunClock>() != null) return;

            var go = new GameObject("RunClock");
            go.hideFlags = HideFlags.HideInHierarchy;
            DontDestroyOnLoad(go);
            go.AddComponent<RunClock>();
        }

        private void Update()
        {
            if (GamePause.IsPaused) return;

            string scene = SceneManager.GetActiveScene().name;
            if (scene == "Menu" || scene == RunSummary.RunEndSceneName) return;

            GameStats.RunPlayTime += Time.unscaledDeltaTime;
        }
    }
}
