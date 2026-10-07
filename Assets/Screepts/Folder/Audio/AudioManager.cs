using UnityEngine;

namespace Tanks2D
{
    public class AudioManager : MonoBehaviour
    {
        // Статическая ссылка, чтобы к звукам можно было обратиться из любого скрипта одной строчкой
        public static AudioManager Instance { get; private set; }

        [Header("Audio Sources (Источники звука)")]
        [SerializeField] private AudioSource _musicSource;
        [SerializeField] private AudioSource _sfxSource;

        [Header("Background Music (Фоновая музыка)")]
        [SerializeField] private AudioClip _backgroundMusic;

        private void Awake()
        {
            // Настройка Синглтона (чтобы менеджер звуков был только один в игре)
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject); // Музыка не прервется при перезапуске уровней
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            // Автоматически запускаем фоновую музыку при старте игры
            PlayMusic(_backgroundMusic);
        }

        // Метод для запуска фоновой музыки
        public void PlayMusic(AudioClip musicClip)
        {
            if (musicClip == null || _musicSource == null) return;

            _musicSource.clip = musicClip;
            _musicSource.loop = true; // Музыка будет играть бесконечно по кругу
            _musicSource.Play();
        }

        // Метод на будущее: для проигрывания звуковых эффектов (выстрел, взрыв, урон)
        public void PlaySFX(AudioClip sfxClip)
        {
            if (sfxClip == null || _sfxSource == null) return;

            // PlayOneShot позволяет звукам накладываться друг на друга (актуально при быстрой стрельбе)
            _sfxSource.PlayOneShot(sfxClip);
        }
    }
}
