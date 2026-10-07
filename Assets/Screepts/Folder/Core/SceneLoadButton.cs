using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Tanks2D
{
    // Кнопка перехода на сцену (лагерь -> бой, game over -> меню и т.п.)
    [RequireComponent(typeof(Button))]
    public class SceneLoadButton : MonoBehaviour
    {
        [SerializeField] private string _sceneName = "Menu";
        [Tooltip("Сбросить прокачку и золото перед переходом")]
        [SerializeField] private bool _resetProgress;

        private void Start()
        {
            GetComponent<Button>().onClick.AddListener(Load);
        }

        public void Load()
        {
            GamePause.Clear();

            if (_resetProgress)
            {
                GameStats.ResetAll();
                Wallet.ResetWallet();
            }

            SceneManager.LoadScene(_sceneName);
        }
    }
}
