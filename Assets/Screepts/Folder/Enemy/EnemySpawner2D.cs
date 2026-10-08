using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

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
        [Tooltip("Ширина линии появления врагов (по центру объекта спавнера). 0 — вся ширина поля")]
        [SerializeField] private float _spawnWidth = 7.6f;
        [Tooltip("Отступ от боковых краёв поля (если ширина линии = 0)")]
        [SerializeField] private float _horizontalMargin = 0.7f;
        [Tooltip("Всегда появляться за верхним краем экрана, даже если спавнер стоит ниже (на длинных телефонах экран выше поля)")]
        [SerializeField] private bool _alwaysAboveScreen = true;
        [Tooltip("На сколько выше верхнего края экрана появляются враги")]
        [SerializeField] private float _spawnAboveScreen = 1f;

        [Header("Boss Settings (Настройки Босса)")]
        [SerializeField] private GameObject bossPrefab;
        [Tooltip("Где появляется босс. Пусто — по центру линии спавна")]
        [SerializeField] private Transform _bossSpawnPoint;
        [Tooltip("Сколько обычных врагов нужно убить, чтобы пришел Босс")]
        [SerializeField] private int killsNeededForBoss = 10;

        [Header("Boss Intro Dialogue (Диалог при появлении босса)")]
        [SerializeField] private DialoguePanel _dialoguePanel;
        [SerializeField] private List<DialogueLine> _bossIntroDialogue = new List<DialogueLine>
        {
            new DialogueLine { speaker = "Босс", portrait = VisualId.Boss, text = "ААА, ООО" },
            new DialogueLine { speaker = "Герой", portrait = VisualId.Player, text = "ЫЫЫЫ", rightSide = true }
        };

        [Header("Enemy Health Scaling (Рост здоровья врагов)")]
        [Tooltip("Прибавка к HP за каждую следующую миссию (0.3 = +30%)")]
        [SerializeField] private float _healthGrowthPerMission = 0.3f;
        [Tooltip("Прибавка к HP за каждую минуту боя (0.5 = +50% в минуту)")]
        [SerializeField] private float _healthGrowthPerMinute = 0.5f;
        [Tooltip("Прибавка к HP за каждый уровень героя (0.15 = +15% за уровень)")]
        [SerializeField] private float _healthGrowthPerPlayerLevel = 0.15f;

        [Header("Transition Settings (Переход после победы)")]
        [FormerlySerializedAs("campSceneName")]
        [SerializeField] private string lobbySceneName = "Lobby";
        [FormerlySerializedAs("delayBeforeCamp")]
        [Tooltip("Пауза после выбора награды за босса перед переходом в лобби, сек.")]
        [SerializeField] private float delayBeforeLobby = 1.5f;

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
        public int BossLevel => GameStats.Mission;
        // Враги крепнут вместе с героем: по миссии, по времени боя и по уровню героя (множители перемножаются)
        public float HealthMultiplier =>
            (1f + _healthGrowthPerMission * Mathf.Max(0, GameStats.Mission - 1)) *
            (1f + _healthGrowthPerMinute * _battleTime / 60f) *
            (1f + _healthGrowthPerPlayerLevel * Mathf.Max(0, GameStats.Level - 1));

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

            Spawn(selectedConfig.enemyPrefab, new Vector3(GetRandomSpawnX(), GetSpawnY(transform.position.y), 0f), false);
        }

        private void SpawnBoss()
        {
            if (bossPrefab == null)
            {
                Debug.LogError("[EnemySpawner2D] Префаб Босса не назначен в инспекторе спавнера!");
                return;
            }

            _isBossSpawned = true;
            Debug.Log($"[Spawner] ВНИМАНИЕ! ПОЯВИЛСЯ БОСС ур. {BossLevel}!");
            PlayMusic(bossMusic);

            Vector3 point = _bossSpawnPoint != null ? _bossSpawnPoint.position : transform.position;
            GameObject boss = Spawn(bossPrefab, new Vector3(point.x, GetSpawnY(point.y), 0f), true);

            BossAbilities abilities = boss.GetComponent<BossAbilities>();
            if (abilities != null) abilities.Setup(BossLevel);

            // Реплики при появлении босса (игра на паузе, пока диалог открыт)
            if (_dialoguePanel != null && _bossIntroDialogue.Count > 0) _dialoguePanel.Play(_bossIntroDialogue, null);
        }

        private GameObject Spawn(GameObject prefab, Vector3 position, bool isBoss)
        {
            GameObject enemyGo = Instantiate(prefab, position, Quaternion.identity);

            PigEnemy enemy = enemyGo.GetComponent<PigEnemy>();
            if (enemy != null)
            {
                enemy.Initialize(isBoss, HealthMultiplier);
                enemy.SetTargetWall(_wall);
            }

            return enemyGo;
        }

        private float GetRandomSpawnX()
        {
            if (_spawnWidth > 0f)
            {
                float half = _spawnWidth * 0.5f;
                return transform.position.x + Random.Range(-half, half);
            }

            PlayField field = PlayField.Instance;
            if (field == null) return transform.position.x;

            float min = field.Left + _horizontalMargin;
            float max = field.Right - _horizontalMargin;
            return min < max ? Random.Range(min, max) : field.Center.x;
        }

        // Высота появления: как стоит спавнер (или точка босса); при _alwaysAboveScreen — не ниже верхнего края экрана
        private float GetSpawnY(float y)
        {
            if (!_alwaysAboveScreen) return y;

            Camera cam = Camera.main;
            if (cam != null && cam.orthographic)
            {
                y = Mathf.Max(y, CameraFitter.GetVisibleWorldRect(cam).yMax + _spawnAboveScreen);
            }

            return y;
        }

        // Линия появления врагов и точка босса видны в окне Scene
        private void OnDrawGizmos()
        {
            Vector3 p = transform.position;
            float half = _spawnWidth > 0f ? _spawnWidth * 0.5f : 4.5f;

            Gizmos.color = new Color(1f, 0.4f, 0.4f);
            Gizmos.DrawLine(p + Vector3.left * half, p + Vector3.right * half);
            Gizmos.DrawLine(p + Vector3.left * half + Vector3.down * 0.3f, p + Vector3.left * half + Vector3.up * 0.3f);
            Gizmos.DrawLine(p + Vector3.right * half + Vector3.down * 0.3f, p + Vector3.right * half + Vector3.up * 0.3f);

            if (_bossSpawnPoint != null)
            {
                Gizmos.color = new Color(1f, 0.2f, 0.3f);
                Gizmos.DrawWireSphere(_bossSpawnPoint.position, 1.2f);
            }
#if UNITY_EDITOR
            UnityEditor.Handles.Label(p + new Vector3(-half, 0.5f, 0f), "Появление врагов");
            if (_bossSpawnPoint != null) UnityEditor.Handles.Label(_bossSpawnPoint.position + new Vector3(-0.4f, 1.5f, 0f), "Босс");
#endif
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
                GameStats.BossesDefeated++;
                GameStats.Mission++;
                Debug.Log("[Spawner] БОСС ПОВЕРЖЕН! Награда и переход в лобби...");

                // Сначала награда (выбор стихии), потом лобби
                if (ExperienceSystem.Instance != null) ExperienceSystem.Instance.OfferBossReward(ScheduleLobby);
                else ScheduleLobby();
                return;
            }

            if (_isBossSpawned) return;

            _currentKillsCount++;
            if (_currentKillsCount >= killsNeededForBoss) SpawnBoss();
        }

        private void ScheduleLobby()
        {
            Invoke(nameof(LoadLobby), delayBeforeLobby);
        }

        private void LoadLobby()
        {
            GamePause.Clear();
            SceneManager.LoadScene(lobbySceneName);
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
