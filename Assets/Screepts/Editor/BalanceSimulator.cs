using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Tanks2D.EditorTools
{
    // Симулятор баланса: бот проходит кампанию с начала в ускоренном времени.
    // Целится во врага, ближайшего к стене, награды выбирает по разумному приоритету.
    // В конце пишет отчёт по уровням: время, убийства, остаток стены, уровень героя, урон.
    // Меню: Tools > Vertical Shooter > Simulate Balance. Командная строка: -executeMethod Tanks2D.EditorTools.BalanceSimulator.RunBatch
    [InitializeOnLoad]
    public static class BalanceSimulator
    {
        private const string ActiveKey = "VerticalShooter.Balance.Active";
        private const string BatchKey = "VerticalShooter.Balance.Batch";
        private const string BattleScenePath = "Assets/Scenes/Vertical/Battle.unity";

        public const int DefaultLevels = 20;
        private const float SimulationSpeed = 4f;
        private const float LevelTimeoutMinutes = 12f;

        // «Человеческий» прицел: цель меняется не каждый кадр, с промахом, не всегда самая срочная
        private const string IdealKey = "VerticalShooter.Balance.Ideal";
        private static bool Ideal => SessionState.GetBool(IdealKey, false);
        private static float RetargetInterval => Ideal ? 0f : 0.6f;
        private static float AimNoiseDegrees => Ideal ? 0f : 8f;
        private static int ThreatChoices => Ideal ? 1 : 3;
        private static float _nextRetarget;
        private static Quaternion _aim = Quaternion.identity;
        private static int _minWallHP;

        // Как выбирает награды «средний разумный игрок»
        private static readonly string[] ChoicePriority =
        {
            "multishot", "damage", "pierce", "firerate", "explosive", "tower", "wall", "repair",
            "towerpower", "reload", "ammo", "spread"
        };

        private static readonly StringBuilder _report = new StringBuilder();
        private static double _waitStart;
        private static float _levelStartTime;
        private static int _currentLevel;
        private static bool _inBattle;
        private static int _maxLevels = DefaultLevels;
        private static FieldInfo _targetRotationField;

        static BalanceSimulator()
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            if (EditorApplication.isPlaying) EditorApplication.delayCall += Begin;
            else EditorApplication.update += WaitAndPlay;
        }

        [MenuItem("Tools/Vertical Shooter/Simulate Balance", priority = 41)]
        public static void Run() => Start(false);

        // Человекоподобный стрелок (по умолчанию)
        public static void RunBatch()
        {
            SessionState.SetBool(IdealKey, false);
            Start(true);
        }

        // Идеальный стрелок: мгновенно и без промаха в самую срочную угрозу
        public static void RunBatchIdeal()
        {
            SessionState.SetBool(IdealKey, true);
            Start(true);
        }

        private static void Start(bool batch)
        {
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetBool(BatchKey, batch);
            EditorSceneManager.OpenScene(BattleScenePath);
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            _waitStart = EditorApplication.timeSinceStartup;
            EditorApplication.update -= WaitAndPlay;
            EditorApplication.update += WaitAndPlay;
        }

        private static void WaitAndPlay()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) { _waitStart = EditorApplication.timeSinceStartup; return; }
            if (EditorApplication.timeSinceStartup - _waitStart < 8) return;

            EditorApplication.update -= WaitAndPlay;
            EditorApplication.isPlaying = true;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode) Begin();
        }

        private static void Begin()
        {
            SaveSystem.UseTemporaryFolder("BalanceSaves");
            GameStats.ResetAll();
            _report.Clear();
            _report.AppendLine("Ур. | время боя | убито | стена (конец, минимум) | ур.героя | урон | пули | башни | итог");
            _currentLevel = 0;
            _inBattle = false;
            _targetRotationField = typeof(PlayerController2D).GetField("_targetRotation", BindingFlags.Instance | BindingFlags.NonPublic);

            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying) return;

            DismissDialogs();
            ChooseRewards();

            // Ускоряем время, когда игра не на паузе
            if (!GamePause.IsPaused) Time.timeScale = SimulationSpeed;

            string scene = SceneManager.GetActiveScene().name;
            bool battle = scene == "Battle";

            if (battle && !_inBattle && EnemySpawner2D.Instance != null)
            {
                _inBattle = true;
                _currentLevel = GameStats.Mission;
                _levelStartTime = Time.time;
                _minWallHP = int.MaxValue;
            }

            if (_inBattle && battle)
            {
                AimAtThreat();
                if (EnemySpawner2D.Instance != null) _lastKills = EnemySpawner2D.Instance.KillsCount;
                if (Wall.ActiveInstance != null)
                {
                    _lastWallHP = Wall.ActiveInstance.CurrentHP;
                    _minWallHP = Mathf.Min(_minWallHP, _lastWallHP);
                }
                _lastBattleMinutes = (Time.time - _levelStartTime) / 60f;

                if (Wall.IsGameOver)
                {
                    bool yog = Object.FindAnyObjectByType<YogSothoth>() != null;
                    bool boss = EnemySpawner2D.Instance != null && EnemySpawner2D.Instance.IsBossSpawned;
                    Record($"ПОРАЖЕНИЕ — стена разрушена{(yog ? " (на поле Йог-Сотот)" : "")}{(boss ? " (бой с боссом)" : "")}, врагов на поле {PigEnemy.AliveCount}");
                    Finish();
                    return;
                }

                if ((Time.time - _levelStartTime) / 60f > LevelTimeoutMinutes)
                {
                    Record($"ТАЙМАУТ — босс не убит за {LevelTimeoutMinutes} мин");
                    Finish();
                    return;
                }
            }

            // Уровень пройден: игра ушла из боя (лобби/деревня)
            if (_inBattle && !battle)
            {
                Record("победа");
                _inBattle = false;

                if (_currentLevel >= _maxLevels || GameStats.CampaignCompleted)
                {
                    Finish();
                    return;
                }

                GamePause.Clear();
                SceneManager.LoadScene("Battle");
            }
        }

        private static int _lastWallHP;
        private static int _lastKills;
        private static float _lastBattleMinutes;

        private static void Record(string result)
        {
            float minutes = _lastBattleMinutes;
            int kills = _lastKills;
            string wallText = $"{_lastWallHP,4}/{GameStats.WallMaxHP,-4} мин {(_minWallHP == int.MaxValue ? 0 : _minWallHP),4}";

            _report.AppendLine($"{_currentLevel,3} | {minutes,6:0.0} мин | {kills,5} | {wallText} | {GameStats.Level,8} | {GameStats.BulletDamage,4} | {GameStats.BulletCount,4} | {GameStats.TowerCount,5} | {result}");
        }

        // Бот целится во врага, ближайшего к стене (самая срочная угроза)
        private static void AimAtThreat()
        {
            var player = Object.FindAnyObjectByType<PlayerController2D>();
            if (player == null || player.Turret == null) return;

            if (Time.time >= _nextRetarget)
            {
                _nextRetarget = Time.time + RetargetInterval;

                Camera cam = Camera.main;
                float screenTop = cam != null ? CameraFitter.GetVisibleWorldRect(cam).yMax : 99f;

                var visible = new List<PigEnemy>();
                foreach (PigEnemy enemy in PigEnemy.Alive)
                {
                    if (!enemy.IsDying && enemy.transform.position.y <= screenTop) visible.Add(enemy);
                }
                if (visible.Count == 0) return;

                // Одна из нескольких ближайших к стене угроз, с промахом
                visible.Sort((a, b) => a.transform.position.y.CompareTo(b.transform.position.y));
                PigEnemy target = visible[Random.Range(0, Mathf.Min(ThreatChoices, visible.Count))];

                Vector2 direction = target.transform.position - player.Turret.position;
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + Random.Range(-AimNoiseDegrees, AimNoiseDegrees);
                _aim = Quaternion.Euler(0f, 0f, Mathf.Clamp(angle, 10f, 170f) - 90f);
            }

            _targetRotationField.SetValue(player, _aim);
        }

        private static void ChooseRewards()
        {
            LevelUpPanel panel = Object.FindAnyObjectByType<LevelUpPanel>();
            int guard = 0;
            while (panel != null && panel.IsOpen && guard++ < 20)
            {
                panel.Choose(PickChoice(panel.CurrentChoices));
            }
        }

        private static int PickChoice(IReadOnlyList<UpgradeChoice> choices)
        {
            foreach (string id in ChoicePriority)
            {
                for (int i = 0; i < choices.Count; i++) if (choices[i].Id == id) return i;
            }
            return 0;
        }

        private static void DismissDialogs()
        {
            DialoguePanel dialogue = Object.FindAnyObjectByType<DialoguePanel>();
            int guard = 0;
            while (dialogue != null && dialogue.IsOpen && guard++ < 20) dialogue.Next();
        }

        private static void Finish()
        {
            EditorApplication.update -= Tick;
            Time.timeScale = 1f;

            Debug.Log($"[BALANCE] {(Ideal ? "идеальный стрелок" : "человекоподобный стрелок")}\n{_report}");

            bool batch = SessionState.GetBool(BatchKey, false);
            SessionState.SetBool(ActiveKey, false);
            GameStats.ResetAll();
            EditorApplication.isPlaying = false;

            if (batch) EditorApplication.delayCall += () => EditorApplication.Exit(0);
        }
    }
}
