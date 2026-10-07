using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Tanks2D.EditorTools
{
    // Быстрая автоматическая проверка боя: запускает сцену Battle в Play Mode,
    // прогоняет опыт/уровни/башни/отскок пуль и пишет отчёт в консоль.
    // Меню: Tools > Vertical Shooter > Run Smoke Test. Из командной строки: -executeMethod Tanks2D.EditorTools.VerticalSmokeTest.RunBatch
    [InitializeOnLoad]
    public static class VerticalSmokeTest
    {
        private const string ActiveKey = "VerticalShooter.SmokeTest.Active";
        private const string BatchKey = "VerticalShooter.SmokeTest.Batch";
        private const string BattleScenePath = "Assets/Scenes/Vertical/Battle.unity";

        private static readonly List<string> _failures = new List<string>();
        private static readonly StringBuilder _report = new StringBuilder();
        private static int _step;
        private static double _stepStart;
        private static int _exceptions;
        private static BulletMovement2D _probeBullet;
        private static float _probeStartDirectionX;
        private static int _maxAliveSeen;

        static VerticalSmokeTest()
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;

            // Сборки перезагрузились уже в Play Mode — продолжаем тест заново
            if (EditorApplication.isPlaying) EditorApplication.delayCall += BeginChecks;
            // Перезагрузка случилась до входа в Play Mode — снова ждём и входим
            else EditorApplication.update += WaitAndEnterPlayMode;
        }

        [MenuItem("Tools/Vertical Shooter/Run Smoke Test", priority = 40)]
        public static void Run()
        {
            Start(false);
        }

        public static void RunBatch()
        {
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
            EditorApplication.update -= WaitAndEnterPlayMode;
            EditorApplication.update += WaitAndEnterPlayMode;
        }

        private static double _waitStart;

        // Ждём, пока редактор закончит компиляцию/импорт: перезагрузка сборок в Play Mode
        // сбрасывает слушатели кнопок и исказила бы результат
        private static void WaitAndEnterPlayMode()
        {
            if (_waitStart <= 0) _waitStart = EditorApplication.timeSinceStartup;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) { _waitStart = EditorApplication.timeSinceStartup; return; }
            if (EditorApplication.timeSinceStartup - _waitStart < 8) return;

            EditorApplication.update -= WaitAndEnterPlayMode;
            EditorApplication.isPlaying = true;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode) return;
            BeginChecks();
        }

        private static void BeginChecks()
        {
            _failures.Clear();
            _report.Clear();
            _step = 0;
            _exceptions = 0;
            _maxAliveSeen = 0;
            _stepStart = EditorApplication.timeSinceStartup;

            Application.logMessageReceived -= OnLog;
            Application.logMessageReceived += OnLog;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private static void OnLog(string condition, string stackTrace, LogType type)
        {
            // Ошибка встроенного поискового индексатора Unity в batch-режиме к игре не относится
            if (stackTrace != null && stackTrace.Contains("UnityEditor.Search")) return;
            if (condition.Contains("UnityEditor.Search")) return;

            if (type == LogType.Exception || type == LogType.Error)
            {
                _exceptions++;
                _report.AppendLine($"  ! {type}: {condition}");
            }
        }

        private static double Elapsed => EditorApplication.timeSinceStartup - _stepStart;

        private static void NextStep()
        {
            _step++;
            _stepStart = EditorApplication.timeSinceStartup;
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying) return;
            _maxAliveSeen = Mathf.Max(_maxAliveSeen, PigEnemy.AliveCount);

            switch (_step)
            {
                case 0: // даём врагам появиться
                    if (Elapsed < 6) return;
                    Check(PigEnemy.AliveCount > 0, $"Враги появляются (живых: {PigEnemy.AliveCount})");
                    Check(FindObjectOf<PlayField>() != null && Camera.main != null, "Есть поле и камера");
                    NextStep();
                    break;

                case 1: // опыт -> уровень -> пауза и окно выбора
                    if (Wall.ActiveInstance != null) Wall.ActiveInstance.UpgradeAndRepair();
                    ExperienceSystem.AddExperience(1000);
                    LevelUpPanel panel = FindObjectOf<LevelUpPanel>();
                    Check(panel != null && panel.IsOpen, "Окно выбора награды открылось");
                    Check(GamePause.IsPaused && Mathf.Approximately(Time.timeScale, 0f), "Игра на паузе во время выбора");
                    Check(GameStats.Level > 1, $"Уровень вырос (уровень {GameStats.Level})");
                    NextStep();
                    break;

                case 2: // разбираем все накопленные уровни: башни, пули, разброс, урон
                {
                    LevelUpPanel panel2 = FindObjectOf<LevelUpPanel>();
                    int damageBefore = GameStats.BulletDamage;
                    int guard = 0;
                    Click("Option_Damage");

                    while (panel2 != null && panel2.IsOpen && guard++ < 100)
                    {
                        if (!Click("Option_Tower") && !Click("Option_MultiShot") && !Click("Option_Spread") && !Click("Option_Damage")) break;
                    }

                    Check(GameStats.TowerCount == GameStats.MaxTowers, $"Построено башен: {GameStats.TowerCount} (максимум {GameStats.MaxTowers})");
                    Check(Object.FindObjectsByType<HelperTower>().Length == GameStats.TowerCount, "Башни есть на сцене");
                    Check(GameStats.BulletCount > 1, $"Пуль в залпе: {GameStats.BulletCount}");
                    Check(GameStats.SpreadAngle > GameStats.BaseSpreadAngle, $"Разброс: {GameStats.SpreadAngle}°");
                    Check(GameStats.BulletDamage > damageBefore, $"Урон вырос: {damageBefore} -> {GameStats.BulletDamage}");
                    Check(!GamePause.IsPaused && Mathf.Approximately(Time.timeScale, 1f), "Пауза снята после выбора");

                    // Пробная пуля летит вверх-влево и должна отскочить от левого борта
                    var player = FindObjectOf<PlayerController2D>();
                    GameObject bulletPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Vertical/Bullet.prefab");
                    PlayerController2D.SpawnBullet(bulletPrefab, new Vector3(-3.5f, -3f, 0f), Quaternion.Euler(0f, 0f, 70f), 10f, 0);
                    _probeBullet = Object.FindObjectsByType<BulletMovement2D>()[0];
                    foreach (BulletMovement2D b in Object.FindObjectsByType<BulletMovement2D>())
                    {
                        if (Vector2.Distance(b.transform.position, new Vector2(-3.5f, -3f)) < 0.5f) _probeBullet = b;
                    }
                    _probeStartDirectionX = _probeBullet.transform.up.x;
                    Check(player != null, "Герой на сцене");
                    NextStep();
                    break;
                }

                case 3: // отскок
                    if (Elapsed < 0.5) return;
                    Check(_probeBullet != null && _probeBullet.transform.up.x > 0f && _probeStartDirectionX < 0f,
                        "Пуля отскочила от левого борта");
                    NextStep();
                    break;

                case 4: // башни воюют, спавн ускоряется со временем
                    if (Elapsed < 10) return;
                    EnemySpawner2D spawner = EnemySpawner2D.Instance;
                    Check(spawner != null && spawner.KillsCount > 0, $"Башни убивают врагов (убито: {spawner?.KillsCount})");
                    Check(_maxAliveSeen <= 50, $"На экране не больше 50 врагов (максимум было {_maxAliveSeen})");
                    Check(spawner != null && spawner.SpawnMultiplier == 1, $"Первая минута: множитель x{spawner?.SpawnMultiplier}");

                    // Перематываем время боя на 2 мин 5 с и разгоняем спавн, чтобы упереться в лимит
                    SetField(spawner, "_battleTime", 125f);
                    Check(spawner.SpawnMultiplier == 4, $"Через 2 минуты множитель x4 (сейчас x{spawner.SpawnMultiplier})");
                    SetField(spawner, "_spawnRate", 0.05f);
                    foreach (HelperTower tower in Object.FindObjectsByType<HelperTower>()) tower.enabled = false;
                    if (Wall.ActiveInstance != null) Wall.ActiveInstance.UpgradeAndRepair();
                    _maxAliveSeen = 0;
                    NextStep();
                    break;

                case 5: // лимит живых врагов
                    if (Wall.ActiveInstance != null) Wall.ActiveInstance.UpgradeAndRepair();
                    if (Elapsed < 4) return;
                    Check(_maxAliveSeen == 50, $"Спавн упирается в лимит 50 врагов (максимум было {_maxAliveSeen})");
                    Finish();
                    break;
            }
        }

        private static void SetField(object target, string field, object value)
        {
            System.Reflection.FieldInfo info = target.GetType().GetField(field, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (info == null) { Check(false, $"Поле {field} не найдено"); return; }
            info.SetValue(target, value);
        }

        private static bool Click(string buttonName)
        {
            foreach (Button button in Object.FindObjectsByType<Button>())
            {
                if (button.name == buttonName && button.gameObject.activeInHierarchy)
                {
                    button.onClick.Invoke();
                    return true;
                }
            }
            return false;
        }

        private static T FindObjectOf<T>() where T : Object
        {
            return Object.FindAnyObjectByType<T>();
        }

        private static void Check(bool condition, string description)
        {
            _report.AppendLine($"  {(condition ? "OK  " : "FAIL")} {description}");
            if (!condition) _failures.Add(description);
        }

        private static void Finish()
        {
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= OnLog;

            Check(_exceptions == 0, $"Нет ошибок и исключений в консоли ({_exceptions})");

            bool passed = _failures.Count == 0;
            Debug.Log($"[SMOKE] {(passed ? "PASSED" : "FAILED")}\n{_report}");

            bool batch = SessionState.GetBool(BatchKey, false);
            SessionState.SetBool(ActiveKey, false);
            EditorApplication.isPlaying = false;

            if (batch) EditorApplication.delayCall += () => EditorApplication.Exit(passed ? 0 : 1);
        }
    }
}
