using UnityEngine;
using UnityEngine.SceneManagement;

namespace Tanks2D
{
    // Спавнит врагов над верхним краем экрана в случайной точке по ширине поля.
    public class EnemySpawner2D : MonoBehaviour
    {
        [System.Serializable]
        public class EnemySpawnConfig
        {
            public string enemyName;
            public GameObject enemyPrefab;
            [Range(0, 100)] public int spawnChance = 50;
            [Min(0)] public int goldReward = 10;
        }

        [Header("References (Ссылки)")]
        [SerializeField] private EnemySpawnConfig[] _enemiesConfigs;
        [SerializeField] private Wall _wall;

        [Header("Spawn Settings (Настройки появления)")]
        [Tooltip("Интервал появления врагов в первую минуту, сек.")]
        [SerializeField] private float _spawnRate = 2f;
        [Tooltip("Каждые столько секунд поток врагов удваивается")]
        [SerializeField] private float _doublingPeriod = 60f;
        [Tooltip("Предел удвоений (6 -> не более x64)")]
        [SerializeField] private int _maxDoublings = 6;
        [Tooltip("Максимум живых врагов на экране одновременно")]
        [SerializeField] private int _maxAliveEnemies = 50;
        [Tooltip("Продолжать спавн обычных врагов, пока жив босс")]
        [SerializeField] private bool _spawnDuringBoss = true;
        [Tooltip("Отступ от боковых краёв поля")]
        [SerializeField] private float _horizontalMargin = 0.7f;
        [Tooltip("На сколько выше верхнего края экрана появляются враги")]
        [SerializeField] private float _spawnAboveScreen = 1f;

        [Header("Boss Settings (Настройки Босса)")]
        [SerializeField] private GameObject bossPrefab;
        [Tooltip("Сколько обычных врагов нужно убить, чтобы пришел Босс")]
        [SerializeField] private int killsNeededForBoss = 10;
        [Tooltip("Сколько золота дадут за убийство Босса")]
        [SerializeField] private int bossGoldReward = 100;

        [Header("Transition Settings (Переход после победы)")]
        [SerializeField] private string campSceneName = "Camp";
        [SerializeField] private float delayBeforeCamp = 3f;

        [Header("Music Settings (Настройки музыки)")]
        [SerializeField] private AudioSource musicAudioSource;
        [SerializeField] private AudioClip normalWaveMusic;
        [SerializeField] private AudioClip bossMusic;

        private float _battleTime;
        private float _spawnProgress;
        private int _currentKillsCount;
        private bool _isBossSpawned;
        private bool _isBossDefeated;

        public static EnemySpawner2D Instance { get; private set; }

        public int KillsCount => _currentKillsCount;
        public int KillsNeededForBoss => killsNeededForBoss;
        public bool IsBossSpawned => _isBossSpawned;
        public bool IsBossDefeated => _isBossDefeated;
        public float BattleTime => _battleTime;

        // x1 в первую минуту, x2 во вторую, x4 в третью...
        public int SpawnMultiplier => 1 << Mathf.Clamp(Mathf.FloorToInt(_battleTime / Mathf.Max(1f, _doublingPeriod)), 0, Mathf.Clamp(_maxDoublings, 0, 20));

        // OnEnable, а не Awake: переживает перезагрузку скриптов прямо в Play Mode
        private void OnEnable()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            if (_wall == null) _wall = Object.FindAnyObjectByType<Wall>();
            if (_wall == null)
            {
                Debug.LogError("[EnemySpawner2D] Ошибка: Спавнер не смог найти объект со скриптом Wall на сцене!");
            }

            PlayMusic(normalWaveMusic);
            _spawnProgress = 0.75f; // первый враг почти сразу
        }

        private void Update()
        {
            if (GamePause.IsPaused || Wall.IsGameOver) return;
            if (_isBossDefeated) return;
            if (_isBossSpawned && !_spawnDuringBoss) return;
            if (_enemiesConfigs == null || _enemiesConfigs.Length == 0) return;

            _battleTime += Time.deltaTime;
            _spawnProgress += Time.deltaTime * SpawnMultiplier / Mathf.Max(0.05f, _spawnRate);

            while (_spawnProgress >= 1f)
            {
                // Лимит на экране: ждём, пока кого-нибудь убьют, не копя очередь
                if (PigEnemy.AliveCount >= _maxAliveEnemies)
                {
                    _spawnProgress = 1f;
                    break;
                }

                SpawnEnemy();
                _spawnProgress -= 1f;
            }
        }

        private void SpawnEnemy()
        {
            EnemySpawnConfig selectedConfig = GetRandomEnemyConfig();
            if (selectedConfig == null || selectedConfig.enemyPrefab == null) return;

            Spawn(selectedConfig.enemyPrefab, GetRandomSpawnX(), selectedConfig.goldReward, false);
        }

        private void SpawnBoss()
        {
            if (bossPrefab == null)
            {
                Debug.LogError("[EnemySpawner2D] Префаб Босса не назначен в инспекторе спавнера!");
                return;
            }

            _isBossSpawned = true;
            Debug.Log("[Spawner] ВНИМАНИЕ! ПОЯВИЛСЯ БОСС!");
            PlayMusic(bossMusic);

            float centerX = PlayField.Instance != null ? PlayField.Instance.Center.x : transform.position.x;
            Spawn(bossPrefab, centerX, bossGoldReward, true);
        }

        private void Spawn(GameObject prefab, float x, int goldReward, bool isBoss)
        {
            var position = new Vector3(x, GetSpawnY(), 0f);
            GameObject enemyGo = Instantiate(prefab, position, Quaternion.identity);

            PigEnemy enemy = enemyGo.GetComponent<PigEnemy>();
            if (enemy != null)
            {
                enemy.Initialize(goldReward, isBoss);
                enemy.SetTargetWall(_wall);
            }
        }

        private float GetRandomSpawnX()
        {
            PlayField field = PlayField.Instance;
            if (field == null) return transform.position.x;

            float min = field.Left + _horizontalMargin;
            float max = field.Right - _horizontalMargin;
            return min < max ? Random.Range(min, max) : field.Center.x;
        }

        // Чуть выше видимой части экрана (на длинных телефонах она выше верхнего края поля)
        private float GetSpawnY()
        {
            float y = transform.position.y;

            Camera cam = Camera.main;
            if (cam != null && cam.orthographic)
            {
                y = Mathf.Max(y, CameraFitter.GetVisibleWorldRect(cam).yMax + _spawnAboveScreen);
            }

            return y;
        }

        private void PlayMusic(AudioClip clip)
        {
            if (musicAudioSource == null || clip == null) return;

            musicAudioSource.Stop();
            musicAudioSource.clip = clip;
            musicAudioSource.loop = true;
            musicAudioSource.Play();
        }

        public void RegisterEnemyDeath(bool isBoss)
        {
            if (isBoss)
            {
                if (_isBossDefeated) return;

                _isBossDefeated = true;
                Debug.Log("[Spawner] БОСС ПОВЕРЖЕН! Переход в лагерь...");
                Invoke(nameof(LoadCampScene), delayBeforeCamp);
                return;
            }

            if (_isBossSpawned) return;

            _currentKillsCount++;
            if (_currentKillsCount >= killsNeededForBoss) SpawnBoss();
        }

        private void LoadCampScene()
        {
            SceneManager.LoadScene(campSceneName);
        }

        private EnemySpawnConfig GetRandomEnemyConfig()
        {
            int totalWeight = 0;
            foreach (var config in _enemiesConfigs)
            {
                if (config.enemyPrefab != null && config.spawnChance > 0) totalWeight += config.spawnChance;
            }

            if (totalWeight == 0) return null;

            int randomWeight = Random.Range(0, totalWeight);
            int currentWeightSum = 0;

            foreach (var config in _enemiesConfigs)
            {
                if (config.enemyPrefab == null || config.spawnChance <= 0) continue;

                currentWeightSum += config.spawnChance;
                if (randomWeight < currentWeightSum) return config;
            }

            return null;
        }
    }
}
