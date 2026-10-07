using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Tanks2D.EditorTools
{
    // Быстрая автоматическая проверка боя: запускает сцену Battle в Play Mode,
    // прогоняет автоатаку, паузу, опыт, стихии, босса и переход в лобби, пишет отчёт в консоль.
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
        private static double _waitStart;
        private static int _exceptions;
        private static int _maxAliveSeen;
        private static BulletMovement2D _probeBullet;
        private static float _probeStartDirectionX;
        private static Vector2 _probeStartVelocity;
        private static float _probeStartGameTime;
        private static PigEnemy _probeEnemy;
        private static float _probeEnemyY;
        private static PigEnemy _boss;
        private static bool _shieldSeen;
        private static bool _retreatSeen;
        private static int _missionBeforeBossKill;
        private static float _bossSpawnGameTime;

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
        public static void Run() => Start(false);

        public static void RunBatch() => Start(true);

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
            _shieldSeen = false;
            _retreatSeen = false;
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
                case 0: // враги появляются, герой стреляет сам
                {
                    if (Elapsed < 6) return;
                    var player = Object.FindAnyObjectByType<PlayerController2D>();
                    Check(PigEnemy.AliveCount > 0 || EnemySpawner2D.Instance.KillsCount > 0, $"Враги появляются (живых: {PigEnemy.AliveCount})");
                    Check(player != null && (player.CurrentAmmo < GameStats.MaxAmmo || player.IsReloading || EnemySpawner2D.Instance.KillsCount > 0),
                        $"Автоатака: герой стреляет без нажатий (патронов {player?.CurrentAmmo}/{GameStats.MaxAmmo})");
                    Check(GameObject.Find("OpenShopButton") == null, "Магазина нет");
                    NextStep();
                    break;
                }

                case 1: // пауза
                {
                    CheckVisualOverrides();

                    var pause = Object.FindAnyObjectByType<PauseMenu>();
                    Check(pause != null, "Есть меню паузы");
                    if (pause != null)
                    {
                        pause.Open();
                        Check(pause.IsOpen && GamePause.IsPaused && Mathf.Approximately(Time.timeScale, 0f), "Пауза включается");
                        pause.Close();
                        Check(!pause.IsOpen && !GamePause.IsPaused && Mathf.Approximately(Time.timeScale, 1f), "Пауза снимается");
                    }
                    NextStep();
                    break;
                }

                case 2: // опыт -> окно наград (без стихий до первого босса)
                {
                    RepairWall();
                    ExperienceSystem.AddExperience(1000);
                    LevelUpPanel panel = Object.FindAnyObjectByType<LevelUpPanel>();
                    Check(panel != null && panel.IsOpen, "Окно выбора награды открылось");
                    Check(GamePause.IsPaused, "Игра на паузе во время выбора");
                    Check(panel != null && panel.CurrentChoices.Count > 0 && panel.CurrentChoices.Count <= 4, $"Вариантов: {panel?.CurrentChoices.Count} (не больше 4)");
                    Check(panel != null && !HasElementChoice(panel), "До первого босса стихии не предлагаются");

                    int before = GameStats.TowerCount + GameStats.BulletCount + GameStats.BulletDamage;
                    int guard = 0;
                    while (panel != null && panel.IsOpen && guard++ < 100) panel.Choose(PreferredIndex(panel, "tower", "multishot", "spread", "damage"));
                    Check(GameStats.TowerCount + GameStats.BulletCount + GameStats.BulletDamage > before,
                        $"Награды применились (башен {GameStats.TowerCount}, пуль {GameStats.BulletCount}, урон {GameStats.BulletDamage})");
                    Check(!GamePause.IsPaused, "Пауза снята после выбора");

                    // Пробная пуля летит вверх-влево и должна отскочить от левого борта
                    GameObject bulletPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Vertical/Bullet.prefab");
                    PlayerController2D.SpawnBullet(bulletPrefab, new Vector3(-3.5f, -3f, 0f), Quaternion.Euler(0f, 0f, 70f), 10f, 0);
                    foreach (BulletMovement2D b in Object.FindObjectsByType<BulletMovement2D>())
                    {
                        if (Vector2.Distance(b.transform.position, new Vector2(-3.5f, -3f)) < 0.5f) _probeBullet = b;
                    }
                    _probeStartDirectionX = _probeBullet != null ? _probeBullet.transform.up.x : 0f;
                    _probeStartVelocity = _probeBullet != null ? _probeBullet.Velocity : Vector2.zero;
                    _probeStartGameTime = Time.time;
                    // Пробная пуля не должна исчезнуть, попав во врага по пути к борту
                    if (_probeBullet != null) SetField(_probeBullet, "_hasHit", true);
                    NextStep();
                    break;
                }

                case 3: // физический отскок
                {
                    if (Time.time - _probeStartGameTime < 0.5f && Elapsed < 30) return;

                    Vector2 after = _probeBullet != null ? _probeBullet.Velocity : Vector2.zero;
                    Check(_probeBullet != null && _probeBullet.Bounces == 1 && _probeStartDirectionX < 0f && after.x > 0f,
                        $"Пуля отскочила от левой стены (отскоков {_probeBullet?.Bounces}, скорость {_probeStartVelocity} -> {after})");
                    Check(Mathf.Abs(Mathf.Abs(after.x) - Mathf.Abs(_probeStartVelocity.x)) < 0.05f && Mathf.Abs(after.y - _probeStartVelocity.y) < 0.05f,
                        "Отражение по физике: угол падения = углу отражения, без самонаведения");
                    Check(Mathf.Abs(after.magnitude - _probeStartVelocity.magnitude) < 0.05f, "Скорость после отскока не теряется");
                    NextStep();
                    break;
                }

                case 4: // стихии: каждая по отдельности
                {
                    AutoChooseLevelUps();
                    RepairWall();
                    ElementCatalog catalog = ElementCatalog.Instance;
                    Check(catalog != null && catalog.Elements.Count >= 4, $"Стихий в каталоге: {catalog?.Elements.Count}");
                    _probeEnemy = FindTestEnemy();
                    if (catalog == null || _probeEnemy == null) { Check(false, "Есть враг для проверки стихий"); NextStep(); break; }

                    foreach (ElementDefinition element in catalog.Elements) element.Level = element.MaxLevel;

                    var hit = new HitContext { Enemy = _probeEnemy, Damage = 10 };
                    Find<EarthElement>(catalog).ModifyHit(ref hit, 1);
                    Check(hit.IsCrit && hit.Damage == 20, $"Земля: крит 10 -> {hit.Damage}");

                    Find<FireElement>(catalog).AfterHit(hit, 1);
                    Check(_probeEnemy.IsBurning, "Огонь: враг горит");

                    float speedBefore = _probeEnemy.CurrentSpeed;
                    Find<IceElement>(catalog).AfterHit(hit, 1);
                    Check(_probeEnemy.IsSlowed && _probeEnemy.CurrentSpeed < speedBefore, $"Холод: скорость {speedBefore:0.0} -> {_probeEnemy.CurrentSpeed:0.0}");

                    _probeEnemyY = _probeEnemy.transform.position.y;
                    Find<WindElement>(catalog).AfterHit(hit, 1);

                    // Все стихии сразу на одной пуле — без ошибок
                    for (int i = 0; i < 30; i++) ElementSystem.ResolveHit(FindTestEnemy(), 1, Vector2.up);
                    NextStep();
                    break;
                }

                case 5: // ветер отбросил, огонь жжёт
                {
                    if (Elapsed < 0.5) return;
                    AutoChooseLevelUps();
                    bool alive = _probeEnemy != null && !_probeEnemy.IsDying;
                    Check(!alive || _probeEnemy.transform.position.y > _probeEnemyY, "Ветер: враг отброшен вверх");
                    Check(!alive || _probeEnemy.CurrentHP < _probeEnemy.MaxHP, "Огонь: горение наносит урон");

                    // Удвоение потока и лимит на экране
                    EnemySpawner2D spawner = EnemySpawner2D.Instance;
                    Check(spawner.SpawnMultiplier == 1, $"Первая минута: множитель x{spawner.SpawnMultiplier}");
                    SetField(spawner, "_battleTime", 125f);
                    Check(spawner.SpawnMultiplier == 4, $"Через 2 минуты множитель x4 (сейчас x{spawner.SpawnMultiplier})");
                    SetField(spawner, "_spawnRate", 0.05f);
                    foreach (HelperTower tower in Object.FindObjectsByType<HelperTower>()) tower.enabled = false;
                    Object.FindAnyObjectByType<PlayerController2D>().enabled = false;
                    _maxAliveSeen = 0;
                    NextStep();
                    break;
                }

                case 6: // лимит живых врагов, затем босс ур. 2
                {
                    RepairWall();
                    AutoChooseLevelUps();
                    if (Elapsed < 4) return;
                    Check(_maxAliveSeen == 50, $"Спавн упирается в лимит 50 врагов (максимум было {_maxAliveSeen})");

                    EnemySpawner2D spawner = EnemySpawner2D.Instance;
                    SetField(spawner, "_spawnRate", 1000f);
                    foreach (PigEnemy enemy in new List<PigEnemy>(PigEnemy.Alive)) Object.Destroy(enemy.gameObject);

                    GameStats.Mission = 2;
                    // Стена с большим запасом: проверяем способности босса, а не баланс
                    GameStats.WallMaxHP = 100000;
                    RepairWall();
                    spawner.GetType().GetMethod("SpawnBoss", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(spawner, null);
                    _boss = null;
                    foreach (BossAbilities abilities in Object.FindObjectsByType<BossAbilities>()) _boss = abilities.GetComponent<PigEnemy>();
                    Check(_boss != null && _boss.GetComponent<BossAbilities>().Level == 2, "Босс ур. 2 появился");

                    // Ставим босса прямо над стеной, чтобы не ждать его подхода
                    if (_boss != null) _boss.transform.position = new Vector3(0f, Wall.ActiveInstance.TopY + 1.25f, 0f);

                    // Диалог при появлении босса
                    var dialogue = Object.FindAnyObjectByType<DialoguePanel>();
                    Check(dialogue != null && dialogue.IsOpen && GamePause.IsPaused, "При появлении босса открылся диалог, игра на паузе");
                    var spoken = new List<string>();
                    int guardDialogue = 0;
                    while (dialogue != null && dialogue.IsOpen && guardDialogue++ < 20)
                    {
                        DialogueLine line = dialogue.CurrentLine;
                        if (line != null && (spoken.Count == 0 || spoken[spoken.Count - 1] != line.speaker + ": " + line.text)) spoken.Add(line.speaker + ": " + line.text);
                        dialogue.Next();
                    }
                    string conversation = string.Join(" / ", spoken);
                    Check(conversation == "Босс: ААА, ООО / Герой: ЫЫЫЫ", $"Реплики: {conversation}");
                    Check(dialogue != null && !dialogue.IsOpen && !GamePause.IsPaused, "Диалог закрылся, игра продолжается");

                    _bossSpawnGameTime = Time.time;
                    NextStep();
                    break;
                }

                case 7: // щит и таран с откатом
                {
                    RepairWall();
                    AutoChooseLevelUps();
                    if (_boss == null) { NextStep(); break; }

                    BossAbilities abilities = _boss.GetComponent<BossAbilities>();
                    if (abilities.ShieldActive && _boss.DamageTakenMultiplier < 1f) _shieldSeen = true;
                    if (abilities.IsRetreating) _retreatSeen = true;
                    // Ждём по игровому времени: в batch-режиме оно может идти медленнее реального
                    float bossGameTime = Time.time - _bossSpawnGameTime;
                    if (bossGameTime < 8f && Elapsed < 60 && !(_shieldSeen && _retreatSeen)) return;

                    Check(_shieldSeen, $"Босс ур. 1: включает щит (урон снижен) — игровое время {bossGameTime:0.0} с, реальное {Elapsed:0.0} с");
                    Check(_retreatSeen, "Босс ур. 2: таран по стене и откат назад");

                    // Стихии были прокачаны до максимума в проверке выше — сбрасываем, чтобы было что предложить
                    foreach (ElementDefinition element in ElementCatalog.Instance.Elements) element.Level = 0;

                    _missionBeforeBossKill = GameStats.Mission;
                    _boss.DamageTakenMultiplier = 1f;
                    _boss.ApplyDamage(1000000);

                    // Сначала идут повышения уровня от опыта босса, затем награда за босса
                    LevelUpPanel panel = Object.FindAnyObjectByType<LevelUpPanel>();
                    ExperienceSystem experience = ExperienceSystem.Instance;
                    int skip = 0;
                    while (panel != null && panel.IsOpen && !experience.IsShowingBossReward && skip++ < 50) panel.Choose(0);

                    Check(panel != null && panel.IsOpen && experience.IsShowingBossReward && OnlyElementChoices(panel) && panel.CurrentChoices.Count == 4,
                        $"Награда за босса: выбор из 4 стихий (вариантов {panel?.CurrentChoices.Count})");
                    Check(GameStats.BossesDefeated == 1 && GameStats.Mission == _missionBeforeBossKill + 1, $"Миссия засчитана (миссия {GameStats.Mission})");
                    if (panel != null && panel.IsOpen) panel.Choose(0);
                    NextStep();
                    break;
                }

                case 8: // переход в лобби
                    AutoChooseLevelUps();
                    if (SceneManager.GetActiveScene().name != "Lobby" && Elapsed < 6) return;
                    Check(SceneManager.GetActiveScene().name == "Lobby", $"После босса — лобби (сцена {SceneManager.GetActiveScene().name})");
                    Check(GameObject.Find("StartMissionButton") != null, "В лобби есть кнопка «Начать миссию»");
                    Finish();
                    break;
            }
        }

        // ---------------------------------------------------------------- Замена оформления

        private static void CheckVisualOverrides()
        {
            var texture = new Texture2D(32, 64);
            Sprite art = Sprite.Create(texture, new Rect(0, 0, 32, 64), new Vector2(0.5f, 0.5f), 32);
            art.name = "TestArt";

            // Свой спрайт
            var go = new GameObject("TestSlot");
            VisualSlot slot = go.AddComponent<VisualSlot>();
            slot.Configure(VisualId.EnemyPig, 10);
            SetField(slot, "_customSprite", art);
            slot.Apply();
            Transform label = go.transform.Find("Label");
            Check(slot.Renderer.sprite == art && slot.Renderer.color == Color.white && (label == null || !label.gameObject.activeSelf),
                $"Свой спрайт виден, подпись-заглушка скрыта ({slot.CurrentSource})");
            Object.Destroy(go);

            // Спрайт, перетащенный вручную в Visual, не затирается
            go = new GameObject("TestSlotManual");
            slot = go.AddComponent<VisualSlot>();
            slot.Configure(VisualId.EnemyPig, 10);
            go.transform.Find("Visual").GetComponent<SpriteRenderer>().sprite = art;
            slot.Apply();
            slot.Apply();
            Check(slot.Renderer.sprite == art, "Спрайт, заданный вручную, сохраняется после Apply");
            Object.Destroy(go);

            // Своя модель (префаб) + свой текст подписи
            var modelTemplate = new GameObject("TestModel");
            modelTemplate.AddComponent<SpriteRenderer>().sprite = art;
            go = new GameObject("TestSlotModel");
            slot = go.AddComponent<VisualSlot>();
            slot.Configure(VisualId.EnemyPig, 10);
            SetField(slot, "_customPrefab", modelTemplate);
            SetField(slot, "_customLabel", "МОЙ ТЕКСТ");
            slot.Apply();
            Transform model = go.transform.Find("Model");
            var body = go.transform.Find("Visual").GetComponent<SpriteRenderer>();
            Bounds bounds = model != null ? model.GetComponentInChildren<SpriteRenderer>().bounds : default;
            var text = go.transform.Find("Label")?.GetComponent<TMPro.TextMeshPro>();
            Check(model != null && !body.enabled && bounds.size.y <= slot.Size.y + 0.01f && bounds.size.y > slot.Size.y * 0.9f,
                $"Своя модель подставлена и вписана в размер ({bounds.size.x:0.00} x {bounds.size.y:0.00}, размер слота {slot.Size.y:0.00})");
            Check(text != null && text.gameObject.activeSelf && text.text == "МОЙ ТЕКСТ", "Свой текст подписи виден поверх модели");
            Object.Destroy(go);
            Object.Destroy(modelTemplate);

            // UI-иконка переключается между id и не залипает на прошлой картинке
            var iconGo = new GameObject("TestIcon", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            UIVisualSlot icon = iconGo.AddComponent<UIVisualSlot>();
            icon.Configure(VisualId.IconDamage);
            icon.Configure(VisualId.IconTower);
            Check(iconGo.GetComponent<UnityEngine.UI.Image>().color == VisualCatalog.Resolve(VisualId.IconTower).color, "Иконка сменилась при смене id");
            Object.Destroy(iconGo);
        }

        // ---------------------------------------------------------------- Helpers

        private static void RepairWall()
        {
            if (Wall.ActiveInstance != null) Wall.ActiveInstance.UpgradeAndRepair();
        }

        // Пока идут проверки, случайные повышения уровня не должны держать игру на паузе
        private static void AutoChooseLevelUps()
        {
            LevelUpPanel panel = Object.FindAnyObjectByType<LevelUpPanel>();
            int guard = 0;
            while (panel != null && panel.IsOpen && guard++ < 50) panel.Choose(0);
        }

        private static PigEnemy FindTestEnemy()
        {
            foreach (PigEnemy enemy in PigEnemy.Alive)
            {
                if (!enemy.IsDying && !enemy.IsBoss) return enemy;
            }
            return null;
        }

        private static T Find<T>(ElementCatalog catalog) where T : ElementDefinition
        {
            foreach (ElementDefinition element in catalog.Elements)
            {
                if (element is T typed) return typed;
            }
            return null;
        }

        private static int PreferredIndex(LevelUpPanel panel, params string[] ids)
        {
            foreach (string id in ids)
            {
                for (int i = 0; i < panel.CurrentChoices.Count; i++)
                {
                    if (panel.CurrentChoices[i].Id == id) return i;
                }
            }
            return 0;
        }

        private static bool HasElementChoice(LevelUpPanel panel)
        {
            foreach (UpgradeChoice choice in panel.CurrentChoices)
            {
                if (choice.Id.StartsWith("element:")) return true;
            }
            return false;
        }

        private static bool OnlyElementChoices(LevelUpPanel panel)
        {
            foreach (UpgradeChoice choice in panel.CurrentChoices)
            {
                if (!choice.Id.StartsWith("element:")) return false;
            }
            return true;
        }

        private static void SetField(object target, string field, object value)
        {
            FieldInfo info = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            if (info == null) { Check(false, $"Поле {field} не найдено"); return; }
            info.SetValue(target, value);
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

            // Тест меняет прогресс — сбрасываем, чтобы не мешать ручной игре
            GameStats.ResetAll();

            bool passed = _failures.Count == 0;
            Debug.Log($"[SMOKE] {(passed ? "PASSED" : "FAILED")}\n{_report}");

            bool batch = SessionState.GetBool(BatchKey, false);
            SessionState.SetBool(ActiveKey, false);
            EditorApplication.isPlaying = false;

            if (batch) EditorApplication.delayCall += () => EditorApplication.Exit(passed ? 0 : 1);
        }
    }
}
