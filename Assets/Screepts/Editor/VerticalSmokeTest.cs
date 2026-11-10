using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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

        private static bool _retreatSeen;
        private static bool _shieldTested;
        private static int _towersAtBossSpawn;
        private static Transform _mountProbe;
        private static Transform _originalMount;
        private static Vector3 _originalTurretPosition;
        private static int _tripPhase;
        private static float _tripPhaseTime;
        private static int _plannedOption;
        private static string _startTownId;
        private static float _rockDistance;
        private static float _logDistance;
        private static bool _jumpedLog;
        private static YogSothoth _yog;
        private static PigEnemy _yogEnemy;
        private static int _yogPhase;
        private static float _yogPhaseStart;
        private static int _towersBeforeYog;
        private static int _regularAfterBoss;
        private static int _missionBeforeBossKill;
        private static float _bossSpawnGameTime;
        private static CharacterAnimations _animProbe;
        private static Sprite[] _animSprites;
        private static float _animStartGameTime;
        private static CharacterAnimations _clipProbe;

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
            // Сохранения теста — во временной папке, не в сохранениях игрока
            SaveSystem.UseTemporaryFolder("SmokeSaves");
            _failures.Clear();
            _report.Clear();
            _step = 0;
            _exceptions = 0;
            _maxAliveSeen = 0;

            _retreatSeen = false;
            _shieldTested = false;
            _tripPhase = 0;
            _tripPhaseTime = 0f;
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
                    Check(PigEnemy.AliveCount + EnemySpawner2D.Instance.KillsCount >= 6, $"Враги идут толпами (появилось {PigEnemy.AliveCount + EnemySpawner2D.Instance.KillsCount} за первые секунды)");
                    int strongest = 0;
                    foreach (PigEnemy enemy in PigEnemy.Alive) strongest = Mathf.Max(strongest, enemy.MaxHP);
                    Check(strongest > 0 && strongest <= GameStats.BulletDamage, $"В начале враги умирают с одного выстрела (самый крепкий: {strongest} HP, урон {GameStats.BulletDamage})");
                    Check(player != null && (player.CurrentAmmo < GameStats.MaxAmmo || player.IsReloading || EnemySpawner2D.Instance.KillsCount > 0),
                        $"Автоатака: герой стреляет без нажатий (патронов {player?.CurrentAmmo}/{GameStats.MaxAmmo})");
                    Check(GameObject.Find("OpenShopButton") == null, "Магазина нет");
                    NextStep();
                    break;
                }

                case 1: // пауза
                {
                    CheckVisualOverrides();
                    CheckLevelsAndExperience();
                    CheckWeaponSkins();
                    CheckQuestRules();
                    CheckArtSlots();
                    CheckGameOverButtons();

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
                    Check(panel != null && panel.CurrentChoices.Count == 5, $"При новом уровне предлагается 5 вариантов ({panel?.CurrentChoices.Count})");
                    Check(panel != null && !HasElementChoice(panel), "До первого босса стихии не предлагаются");

                    int before = GameStats.TowerCount + GameStats.BulletCount + GameStats.BulletDamage;
                    int guard = 0;
                    while (panel != null && panel.IsOpen && guard++ < 100) panel.Choose(PreferredIndex(panel, "tower", "multishot", "spread", "damage"));
                    Check(GameStats.TowerCount + GameStats.BulletCount + GameStats.BulletDamage > before,
                        $"Награды применились (башен {GameStats.TowerCount}, пуль {GameStats.BulletCount}, урон {GameStats.BulletDamage})");
                    Check(!GamePause.IsPaused, "Пауза снята после выбора");
                    CheckSkillPool();

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
                    StartAnimationProbe();
                    StartMountProbe();

                    // Башни встали в слоты-объекты сцены
                    TowerManager towerManager = Object.FindAnyObjectByType<TowerManager>();
                    bool towersOnSlots = towerManager != null && towerManager.Towers.Count == GameStats.TowerCount;
                    for (int i = 0; towersOnSlots && i < towerManager.Towers.Count; i++)
                    {
                        towersOnSlots = Vector2.Distance(towerManager.Towers[i].transform.position, towerManager.GetSlotPosition(i)) < 0.01f;
                    }
                    Check(towersOnSlots && GameObject.Find("Slot_1") != null, $"Башни стоят в слотах Slot_1..{towerManager?.Towers.Count}");
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
                    CheckAnimationProbe();
                    CheckMountProbe();
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
                    CheckPierceAndExplosion();
                    StartBodyProbe();
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
                    CheckBodyProbe();
                    if (_animProbe != null)
                    {
                        Check(_animProbe.Current == CharacterAnimation.Death && _animProbe.GetComponent<VisualSlot>().Renderer.sprite == _animSprites[5],
                            "Анимация смерти доиграла и замерла на последнем кадре");
                        Object.Destroy(_animProbe.gameObject);
                    }

                    // Удвоение потока и лимит на экране
                    EnemySpawner2D spawner = EnemySpawner2D.Instance;
                    Check(spawner.SpawnMultiplier == 1, $"Первая минута: множитель x{spawner.SpawnMultiplier}");

                    // Здоровье: первая минута льготная, дальше экспонента ×1.4 в минуту
                    float realTime = spawner.BattleTime;
                    SetField(spawner, "_battleTime", 0f);
                    float h0 = spawner.HealthMultiplier;
                    SetField(spawner, "_battleTime", 60f);
                    float h1 = spawner.HealthMultiplier;
                    SetField(spawner, "_battleTime", 180f);
                    float h3 = spawner.HealthMultiplier;
                    SetField(spawner, "_battleTime", 300f);
                    float h5 = spawner.HealthMultiplier;
                    Check(Mathf.Approximately(h0, h1) && Mathf.Abs(h3 / h0 - 1.96f) < 0.01f && Mathf.Abs(h5 / h3 - 1.96f) < 0.01f,
                        $"Здоровье врагов: 1-я минута без роста, дальше экспонента (x{h0:0.00} -> 1 мин x{h1:0.00} -> 3 мин x{h3:0.00} -> 5 мин x{h5:0.00})");
                    SetField(spawner, "_battleTime", realTime);

                    SetField(spawner, "_battleTime", 125f);
                    Check(spawner.SpawnMultiplier == 4, $"Через 2 минуты множитель x4 (сейчас x{spawner.SpawnMultiplier})");
                    SetField(spawner, "_spawnRate", 0.05f);
                    foreach (HelperTower tower in Object.FindObjectsByType<HelperTower>()) tower.enabled = false;
                    Object.FindAnyObjectByType<PlayerController2D>().enabled = false;
                    _maxAliveSeen = 0;
                    NextStep();
                    break;
                }

                case 6: // Йог-Сотот
                    StartYogTest();
                    NextStep();
                    break;

                case 7:
                    RepairWall();
                    AutoChooseLevelUps();
                    if (TickYogTest()) NextStep();
                    break;

                case 8: // лимит живых врагов, затем босс ур. 2
                {
                    RepairWall();
                    AutoChooseLevelUps();
                    if (Elapsed < 4) return;
                    EnemySpawner2D spawner = EnemySpawner2D.Instance;
                    int cap = (int)GetField(spawner, "_maxAliveEnemies");
                    Check(cap >= 80 && _maxAliveSeen >= cap, $"Спавн упирается в лимит {cap} врагов (максимум было {_maxAliveSeen})");
                    SetField(spawner, "_spawnRate", 1000f);

                    bool onSpawnLine = true;
                    foreach (PigEnemy enemy in PigEnemy.Alive)
                    {
                        if (Mathf.Abs(enemy.transform.position.x - spawner.transform.position.x) > 3.81f) onSpawnLine = false;
                    }
                    Check(onSpawnLine, "Враги появляются в пределах линии спавна");
                    CheckEnemyPool();

                    Transform bossPoint = spawner.transform.Find("BossSpawnPoint");
                    if (bossPoint != null) bossPoint.position = new Vector3(2f, bossPoint.position.y, 0f);

                    GameStats.Mission = 2;
                    // Стена с большим запасом: проверяем способности босса, а не баланс
                    GameStats.WallMaxHP = 100000;
                    RepairWall();

                    // Уровень 2 (копия, чтобы не менять ассет) + мини-босс после следующего убийства
                    LevelDefinition level2 = Object.Instantiate(LevelCatalog.Instance.Get(2));
                    GameObject pigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Vertical/Enemy_Pig.prefab");
                    level2.miniBosses.Add(new MiniBossSpawn { prefab = pigPrefab, afterKills = spawner.KillsCount + 1, healthMultiplier = 3f });
                    SetField(spawner, "_level", level2);
                    int aliveBefore = PigEnemy.AliveCount;
                    spawner.RegisterEnemyDeath(false);
                    PigEnemy mini = PigEnemy.Alive.Count > 0 ? PigEnemy.Alive[PigEnemy.Alive.Count - 1] : null;
                    Check(PigEnemy.AliveCount == aliveBefore + 1 && mini != null && mini.Difficulty >= 2.9f,
                        $"Мини-босс из настроек уровня появился (сложность x{mini?.Difficulty:0.0})");
                    if (mini != null) Object.Destroy(mini.gameObject);

                    // После проверки Йога башен может не остаться — восстанавливаем для проверки босса
                    while (TowerManager.Instance != null && TowerManager.Instance.CanAddTower) TowerManager.Instance.AddTower();
                    _towersAtBossSpawn = GameStats.TowerCount;
                    int regularBefore = 0;
                    foreach (PigEnemy enemy in PigEnemy.Alive) if (!enemy.IsBoss) regularBefore++;
                    spawner.GetType().GetMethod("SpawnBoss", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(spawner, null);
                    _boss = null;
                    foreach (BossAbilities abilities in Object.FindObjectsByType<BossAbilities>()) _boss = abilities.GetComponent<PigEnemy>();
                    Check(_boss != null && _boss.GetComponent<BossAbilities>().Level == 2, "Босс ур. 2 появился");
                    BossAbility bossFlags = _boss != null ? _boss.GetComponent<BossAbilities>().Abilities : BossAbility.None;
                    Check(bossFlags == (BossAbility.Shield | BossAbility.RamWall | BossAbility.DestroyTowers), $"Способности босса из уровня 2: {bossFlags}");
                    Check(_boss != null && Mathf.Abs(_boss.transform.position.x - 2f) < 0.01f, $"Босс появился в точке BossSpawnPoint (x = {_boss?.transform.position.x:0.00})");
                    int regularLeft = 0;
                    foreach (PigEnemy enemy in PigEnemy.Alive) if (!enemy.IsBoss) regularLeft++;
                    _regularAfterBoss = regularLeft;
                    Check(regularLeft == regularBefore && regularLeft > 0 && _boss != null && !_boss.IsDying,
                        $"При появлении босса уже вышедшие враги остаются в бою ({regularLeft} из {regularBefore})");
                    Check(_boss != null && _boss.GetComponent<CharacterAnimations>() != null, "У босса есть компонент анимаций");

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

                case 9: // щит и таран с откатом
                {
                    RepairWall();
                    AutoChooseLevelUps();
                    if (_boss == null) { NextStep(); break; }

                    BossAbilities abilities = _boss.GetComponent<BossAbilities>();
                    if (abilities.ShieldActive && !_shieldTested) TestShield(abilities);
                    if (abilities.IsRetreating) _retreatSeen = true;
                    // Ждём по игровому времени: в batch-режиме оно может идти медленнее реального
                    float bossGameTime = Time.time - _bossSpawnGameTime;
                    bool done = _shieldTested && _retreatSeen && abilities.TowersDestroyed > 0;
                    if (bossGameTime < 14f && Elapsed < 90 && !done) return;

                    Check(_shieldTested, $"Босс включает щит — игровое время {bossGameTime:0.0} с");
                    Check(_retreatSeen, "Босс ур. 2: таран по стене и откат назад");
                    int regularDuringBoss = 0;
                    foreach (PigEnemy enemy in PigEnemy.Alive) if (!enemy.IsBoss) regularDuringBoss++;
                    Check(regularDuringBoss <= _regularAfterBoss, $"Пока жив босс, новые враги не появляются (было {_regularAfterBoss}, стало {regularDuringBoss})");
                    Check(abilities.TowersDestroyed > 0 && GameStats.TowerCount == _towersAtBossSpawn - abilities.TowersDestroyed,
                        $"Босс ур. 2 разрушает башни (разрушено {abilities.TowersDestroyed}, башен было {_towersAtBossSpawn}, осталось {GameStats.TowerCount})");

                    Check(GameFeel.ShakeCount > 0 && GameFeel.BurstCount > 0 && Camera.main.GetComponent<CameraShake>() != null,
                        $"Ощущение от ударов: тряска камеры ({GameFeel.ShakeCount} раз), осколки ({GameFeel.BurstCount} вспышек)");

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

                case 10: // после босса — деревня
                {
                    AutoChooseLevelUps();
                    if (SceneManager.GetActiveScene().name != "Village" && Elapsed < 6) return;
                    Check(SceneManager.GetActiveScene().name == "Village", $"После босса — деревня (сцена {SceneManager.GetActiveScene().name})");
                    if (Elapsed < 0.5) return;

                    CheckVillage();
                    NextStep();
                    break;
                }

                case 11: // из деревни — в лобби
                    if (SceneManager.GetActiveScene().name != "Lobby" && Elapsed < 6) return;
                    Check(SceneManager.GetActiveScene().name == "Lobby", $"Кнопка «Продолжить путь» ведёт в лобби (сцена {SceneManager.GetActiveScene().name})");
                    Check(GameObject.Find("StartMissionButton") != null, "В лобби есть кнопка «Начать миссию»");
                    StartTravelTest();
                    NextStep();
                    break;

                case 12: // деревня: уровни города пройдены — путь на карту
                    if (SceneManager.GetActiveScene().name != "Village" && Elapsed < 6) return;
                    if (Elapsed < 0.5) return;
                    CheckVillageLeadsToMap();
                    NextStep();
                    break;

                case 13: // карта похода
                    if (SceneManager.GetActiveScene().name != "Map" && Elapsed < 6) return;
                    if (Elapsed < 0.5) return;
                    CheckMapScreen();
                    NextStep();
                    break;

                case 14: // дилижанс: управление, авария, встреча
                    if (SceneManager.GetActiveScene().name != "Stagecoach" && Elapsed < 6) return;
                    if (Elapsed < 0.5) return;
                    if (!TickStagecoach()) return;
                    NextStep();
                    break;

                case 15: // приезд в новый город
                    if (SceneManager.GetActiveScene().name != "Village" && Elapsed < 10) return;
                    if (Elapsed < 0.5) return;
                    CheckArrival();
                    CheckSaveAndLoad();
                    // Quest Tester начинает новый забег — после проверки возвращаем сохранённый
                    CheckQuestTester();
                    SaveSystem.LoadRun();
                    SceneManager.LoadScene("Menu");
                    NextStep();
                    break;

                case 16: // главное меню: «Продолжить» поднимает забег с диска
                {
                    if (SceneManager.GetActiveScene().name != "Menu" && Elapsed < 6) return;
                    if (Elapsed < 0.5) return;
                    var menu = Object.FindAnyObjectByType<MainMenuController>();
                    Button load = GameObject.Find("LoadGameButton")?.GetComponent<Button>();
                    Check(menu != null && menu.CanContinue && load != null && load.interactable, "В главном меню доступно «Продолжить»");
                    Check(Object.FindAnyObjectByType<LegacyPanel>() != null && GameObject.Find("LegacyButton") != null, "В главном меню есть «Наследие»");
                    _savedMission = GameStats.Mission;
                    GameStats.Mission = 25; // в памяти другое — после «Продолжить» должно вернуться сохранённое
                    menu?.ContinueGame();
                    NextStep();
                    break;
                }

                case 17:
                    if (SceneManager.GetActiveScene().name != "Village" && Elapsed < 6) return;
                    if (Elapsed < 0.5) return;
                    Check(SceneManager.GetActiveScene().name == "Village" && GameStats.Mission == _savedMission,
                        $"«Продолжить» вернул забег на тот же экран (сцена {SceneManager.GetActiveScene().name}, уровень {GameStats.Mission})");
                    _gloryBefore = MetaProgress.Glory;
                    RunSummary.Finish(false);
                    NextStep();
                    break;

                case 18: // итоги забега и наследие
                    if (SceneManager.GetActiveScene().name != RunSummary.RunEndSceneName && Elapsed < 6) return;
                    if (Elapsed < 0.5) return;
                    CheckRunEnd();
                    Finish();
                    break;
            }
        }

        // ---------------------------------------------------------------- Карта похода и дилижанс

        private static void StartTravelTest()
        {
            // Генератор карты: 10 рядов, старт и финал по одному городу, в середине по 2–3, все города достижимы
            TownCatalog towns = TownCatalog.Instance;
            Check(towns != null && towns.Towns.Count == 30, $"Пул городов: {towns?.Towns.Count} (нужно 30)");

            bool valid = true;
            bool storyTownsInPlace = true;
            string problem = "";
            var storyTowns = new List<string>();
            foreach (TownDefinition town in towns.Towns)
            {
                if (town != null && town.fixedMapRow >= 0) storyTowns.Add($"{town.displayName} — ряд {town.fixedMapRow + 1}");
            }

            for (int seed = 1; seed <= 50 && valid; seed++)
            {
                RunMap test = RunMapGenerator.Generate(seed, towns.Towns);
                foreach (TownDefinition town in towns.Towns)
                {
                    if (town == null || town.fixedMapRow < 0) continue;
                    storyTownsInPlace &= test.rows[town.fixedMapRow].nodes.Exists(n => n.townId == town.Id);
                }
                var ids = new HashSet<string>();
                int total = 0;
                for (int r = 0; r < test.RowCount; r++)
                {
                    int count = test.rows[r].nodes.Count;
                    bool edgeRow = r == 0 || r == test.RowCount - 1;
                    if (edgeRow ? count != 1 : count < 2 || count > 3) { valid = false; problem = $"ряд {r}: {count} городов"; }

                    foreach (MapNode node in test.rows[r].nodes)
                    {
                        total++;
                        ids.Add(node.townId);
                        if (r < test.RowCount - 1 && node.next.Count == 0) { valid = false; problem = $"тупик {r}/{node.index}"; }
                        if (r > 0)
                        {
                            bool reached = false;
                            foreach (MapNode prev in test.rows[r - 1].nodes) reached |= prev.next.Contains(node.index);
                            if (!reached) { valid = false; problem = $"недостижим {r}/{node.index}"; }
                        }
                    }
                }
                if (test.RowCount != 10 || ids.Count != total) { valid = false; problem = $"рядов {test.RowCount}, уникальных городов {ids.Count} из {total}"; }
            }
            Check(valid, $"Карта забега: 10 рядов, развилки 2–3, все города достижимы и без повторов (50 карт){(valid ? "" : " — " + problem)}");

            Check(storyTownsInPlace && storyTowns.Count >= 2,
                $"Сюжетные города всегда стоят на своих рядах карты ({string.Join(", ", storyTowns)})");

            RunMap a = RunMapGenerator.Generate(7, towns.Towns), b = RunMapGenerator.Generate(8, towns.Towns);
            Check(MapSignature(a) != MapSignature(b), "Каждый забег — своя карта");

            // Новый забег; пройдены уровни 1–3 первого города — путь лежит в дорогу
            GameStats.Map = null;
            GameStats.Mission = 4;
            GameStats.CurrentTownId = null;
            _startTownId = CampaignRun.CurrentTown?.Id;
            SceneManager.LoadScene("Village");
        }

        private static string MapSignature(RunMap map)
        {
            var signature = new StringBuilder();
            foreach (MapRow row in map.rows)
            {
                foreach (MapNode node in row.nodes) signature.Append(node.townId).Append(':').Append(string.Join(",", node.next)).Append(' ');
                signature.Append('|');
            }
            return signature.ToString();
        }

        private static void CheckVillageLeadsToMap()
        {
            var village = Object.FindAnyObjectByType<VillageController>();
            Check(village != null && village.Town != null && village.Town.Id == _startTownId,
                $"Деревня стартового города карты ({village?.Town?.displayName})");
            Check(village != null && village.LeadsToMap, "Уровни города пройдены — «Продолжить» ведёт в дорогу");
            village?.Continue();
        }

        private static void CheckMapScreen()
        {
            var view = Object.FindAnyObjectByType<RunMapView>();
            RunMap map = CampaignRun.Map;
            int total = 0;
            foreach (MapRow row in map.rows) total += row.nodes.Count;
            Check(view != null && view.NodeCount == total && view.EdgeCount > 0, $"Карта похода: {view?.NodeCount} городов, {view?.EdgeCount} дорог");

            // Намечаем город, где ждёт задание (если его нет среди дорог — последний доступный)
            List<MapNode> options = map.NextOptions;
            Check(options.Count >= 2, $"Из стартового города {options.Count} дороги");
            QuestDefinition quest = QuestCatalog.Instance != null ? QuestCatalog.Instance.Find(QuestId) : null;
            _plannedOption = options.Count - 1;
            for (int i = 0; i < options.Count; i++)
            {
                if (quest != null && quest.town != null && options[i].townId == quest.town.Id) _plannedOption = i;
            }
            Check(quest != null && quest.town != null && options[_plannedOption].townId == quest.town.Id,
                $"С первой развилки есть дорога в город задания «{quest?.town?.displayName}»");
            view.Plan(options[_plannedOption]);
            Check(map.plannedIndex == options[_plannedOption].index, "Город на карте намечается нажатием");
            view.Travel();
        }

        // true — проверка дилижанса закончена
        private static bool TickStagecoach()
        {
            StagecoachRun run = StagecoachRun.Instance;
            if (run == null) { Check(false, "Дилижанс запустился"); return true; }
            float t = Time.time - _tripPhaseTime;

            switch (_tripPhase)
            {
                case 0:
                    Check(run.Options.Count >= 2 && run.Wheels == 3, $"Поездка: {run.Options.Count} города на развилке, колёс {run.Wheels}");
                    int lane = run.Lane;
                    run.ChangeLane(-1);
                    Check(run.Lane == Mathf.Max(0, lane - 1), "Смена полосы");
                    run.Jump();
                    Check(run.IsAirborne, "Прыжок");

                    // Настоящие препятствия в полосе дилижанса: камень (не перепрыгнуть) и бревно (перепрыгнуть)
                    MethodInfo add = typeof(StagecoachRun).GetMethod("AddObject", BindingFlags.Instance | BindingFlags.NonPublic);
                    System.Type objectType = typeof(StagecoachRun.RoadObjectType);
                    _rockDistance = run.Distance + 2f;
                    _logDistance = run.Distance + 9f;
                    add.Invoke(run, new object[] { System.Enum.Parse(objectType, "Rock"), run.Lane, _rockDistance });
                    add.Invoke(run, new object[] { System.Enum.Parse(objectType, "Log"), run.Lane, _logDistance });
                    _jumpedLog = false;
                    NextTripPhase();
                    return false;

                case 1: // камень — минус колесо, бревно в прыжке — без потерь
                    if (run.Distance < _rockDistance + 0.5f && t < 6f) return false;
                    if (run.Wheels == 3 || (run.Distance < _logDistance - 1.2f && t < 6f))
                    {
                        if (run.Distance >= _rockDistance + 0.5f && run.Wheels == 3) { Check(false, "Камень на полосе отнимает колесо"); NextTripPhase(); }
                        return false;
                    }
                    if (!_jumpedLog)
                    {
                        Check(run.Wheels == 2, $"Камень на полосе отнимает колесо (колёс {run.Wheels})");
                        run.Jump();
                        _jumpedLog = true;
                        return false;
                    }
                    if (run.Distance < _logDistance + 0.5f && t < 8f) return false;
                    Check(run.Wheels == 2, $"Бревно перепрыгнуто — колёса целы ({run.Wheels})");
                    NextTripPhase();
                    return false;

                case 2: // дорога едет, скорость растёт
                    Check(run.Distance > 2f, $"Дилижанс едет (проехано {run.Distance:0.0})");

                    // Авария: три удара — колёс нет, стена в следующем бою повреждена
                    MethodInfo crash = typeof(StagecoachRun).GetMethod("Crash", BindingFlags.Instance | BindingFlags.NonPublic);
                    for (int i = 0; i < 3; i++) crash.Invoke(run, null);
                    Check(run.Wheels == 0 && run.IsBrokenDown && GameStats.NextBattleWallDamage > 0f,
                        $"Без колёс дилижанс едет дальше, но стена пострадает ({GameStats.NextBattleWallDamage * 100:0}%)");

                    // Перематываем к путнику
                    run.SkipTo(run.TripLength * 0.3f - 0.3f);
                    NextTripPhase();
                    return false;

                case 3: // встреча (Hand of Fate)
                {
                    EncounterPanel panel = run.Encounters;
                    if ((panel == null || !panel.IsOpen) && t < 5f) return false;
                    Check(panel != null && panel.IsOpen && panel.Current != null && GamePause.IsPaused,
                        $"Встреча в пути: «{panel?.Current?.title}», игра на паузе");
                    if (panel == null || !panel.IsOpen) { NextTripPhase(); return false; }

                    // Выбираем вариант с риском, если он есть
                    int gamble = panel.Current.choices.FindIndex(c => c.isGamble);
                    int choice = gamble >= 0 ? gamble : panel.Current.choices.FindIndex(c => c.horseshoeCost <= GameStats.Horseshoes);
                    panel.Choose(Mathf.Max(0, choice));

                    if (gamble >= 0)
                    {
                        Check(panel.CurrentStage == EncounterPanel.Stage.Chance && panel.ChanceCardCount >= 4,
                            $"Карты удачи перемешаны рубашкой вверх ({panel.ChanceCardCount})");
                        panel.PickChance(0);
                        Check(panel.LastDrawn != null, $"Вытянута карта: {panel.LastDrawn?.result}");
                    }

                    Check(panel.CurrentStage == EncounterPanel.Stage.Result, "Итог встречи показан");
                    panel.Continue();
                    Check(!panel.IsOpen && !GamePause.IsPaused, "Встреча закрыта, дорога продолжается");

                    // Всегда проверяем и риск с картами удачи (встреча в дороге случайная)
                    EncounterDefinition risky = null;
                    foreach (EncounterDefinition e in EncounterCatalog.Instance.Encounters) if (risky == null && e.choices.Exists(c => c.isGamble)) risky = e;
                    panel.Show(risky, null);
                    panel.Choose(risky.choices.FindIndex(c => c.isGamble));
                    Check(panel.CurrentStage == EncounterPanel.Stage.Chance && panel.ChanceCardCount >= 4,
                        $"«{risky.title}»: карты удачи перемешаны рубашкой вверх ({panel.ChanceCardCount})");
                    panel.PickChance(panel.ChanceCardCount - 1);
                    Check(panel.LastDrawn != null && panel.CurrentStage == EncounterPanel.Stage.Result, $"Вытянута карта удачи: {panel.LastDrawn?.result}");
                    panel.Continue();

                    // Эффект колёс от встреч (починка)
                    EncounterEffects.Apply(new EncounterEffect { type = EncounterEffectType.Wheels, amount = 1 });
                    Check(run.Wheels == 1, $"Встреча может починить колесо (колёс {run.Wheels})");

                    // К развилке, в полосу намеченного города
                    int targetLane = run.Options.Count == 2 ? (_plannedOption == 0 ? 0 : 2) : _plannedOption;
                    while (run.Lane > targetLane) run.ChangeLane(-1);
                    while (run.Lane < targetLane) run.ChangeLane(1);
                    run.SkipTo(run.TripLength - 1f);
                    NextTripPhase();
                    return false;
                }

                case 4: // развилка
                    if (!run.Finished && t < 5f) return false;
                    Check(run.Finished && run.ChosenOption == _plannedOption, $"Развилка: выбран город по полосе (вариант {run.ChosenOption})");
                    return true;
            }

            return true;
        }

        private static void NextTripPhase()
        {
            _tripPhase++;
            _tripPhaseTime = Time.time;
        }

        private static void CheckArrival()
        {
            RunMap map = CampaignRun.Map;
            var village = Object.FindAnyObjectByType<VillageController>();
            Check(map.currentRow == 1 && map.IsVisited(1, map.currentIndex), $"Герой переехал во 2-й ряд карты (город {map.currentIndex})");
            Check(village != null && village.Town != null && village.Town.Id != _startTownId && village.Town.Id == CampaignRun.CurrentTown.Id,
                $"Деревня нового города: {village?.Town?.displayName}");
            Check(village != null && !village.LeadsToMap, "В новом городе «Продолжить» ведёт к бою (уровни 4–6)");

            CheckQuestInVillage(village);
        }

        // ---------------------------------------------------------------- Сохранение, итоги, наследие

        private static int _savedMission;
        private static int _gloryBefore;

        private static void CheckGameOverButtons()
        {
            var wall = Wall.ActiveInstance;
            var panel = wall != null ? (GameObject)GetField(wall, "gameOverPanel") : null;
            RunFlowButton[] buttons = panel != null ? panel.GetComponentsInChildren<RunFlowButton>(true) : new RunFlowButton[0];
            bool retry = System.Array.Exists(buttons, b => b.Kind == RunFlowButton.Action.RetryLevel);
            bool finish = System.Array.Exists(buttons, b => b.Kind == RunFlowButton.Action.FinishRun);
            Check(retry && finish, "Экран поражения: «Повторить уровень» и «Завершить поход»");
        }

        private static void CheckSaveAndLoad()
        {
            RunSaveData saved = SaveSystem.ReadRun();
            Check(SaveSystem.HasRun && saved != null && saved.scene == "Village" && saved.runId == GameStats.RunId,
                $"Забег сохранён на диск при входе в деревню ({saved?.scene}, {saved?.savedAt})");

            int mission = GameStats.Mission, horseshoes = GameStats.Horseshoes, row = CampaignRun.Map.currentRow, kills = GameStats.RunKills;
            string town = CampaignRun.CurrentTown?.Id;
            int flags = GameStats.StoryFlags.Count;

            // Портим прогресс в памяти — загрузка должна вернуть всё как было
            GameStats.Mission = 29;
            GameStats.Horseshoes += 50;
            GameStats.Map = null;
            GameStats.SetStoryFlag("smoke_tmp");
            string scene = SaveSystem.LoadRun();

            Check(scene == "Village" && GameStats.Mission == mission && GameStats.Horseshoes == horseshoes && GameStats.Map != null
                  && CampaignRun.Map.currentRow == row && CampaignRun.CurrentTown?.Id == town
                  && GameStats.StoryFlags.Count == flags && !GameStats.HasStoryFlag("smoke_tmp") && GameStats.RunKills == kills,
                $"Загрузка возвращает прогресс: уровень {GameStats.Mission}, город {CampaignRun.CurrentTown?.displayName}, подковы {GameStats.Horseshoes}, флаги {GameStats.StoryFlags.Count}, убито {GameStats.RunKills}");

            // Брошенный бой откатывается к сохранению, статистика остаётся
            GameStats.InBattle = true;
            GameStats.Mission = 17;
            GameStats.RunKills += 5;
            bool reverted = SaveSystem.RevertUnfinishedBattle();
            Check(reverted && GameStats.Mission == mission && GameStats.RunKills == kills + 5 && !GameStats.InBattle,
                "Брошенный бой (выход или поражение) откатывает прогресс к сохранению перед боем, статистика остаётся");

            GameStats.RunId++;
            GameStats.InBattle = true;
            GameStats.Mission = 17;
            Check(!SaveSystem.RevertUnfinishedBattle() && GameStats.Mission == 17, "Чужое сохранение (другой забег) не подменяет прогресс");
            GameStats.RunId--;
            GameStats.Mission = mission;
        }

        private static void CheckRunEnd()
        {
            var screen = Object.FindAnyObjectByType<RunEndScreen>();
            RunResult result = screen != null ? screen.Result : null;
            Check(result != null && !result.victory && result.levels >= 3 && result.kills > 0 && result.glory > 0,
                $"Итоги похода: уровней {result?.levels}, убито {result?.kills}, городов {result?.towns}, заданий {result?.quests}, слава +{result?.glory}");
            Check(result != null && MetaProgress.Glory == _gloryBefore + result.glory && !SaveSystem.HasRun,
                $"Слава ушла в наследие ({_gloryBefore} -> {MetaProgress.Glory}), сохранение забега удалено");

            LegacyPanel legacy = Object.FindAnyObjectByType<LegacyPanel>();
            MetaUpgradeCatalog catalog = MetaUpgradeCatalog.Instance;
            legacy?.Open();
            Check(legacy != null && legacy.IsOpen && catalog != null && legacy.Rows.Count == catalog.Upgrades.Count && catalog.Upgrades.Count >= 4,
                $"Окно «Наследие»: {legacy?.Rows.Count} улучшений");

            MetaProgress.AddGlory(1000);
            int glory = MetaProgress.Glory;
            int wallIndex = catalog != null ? new List<MetaUpgrade>(catalog.Upgrades).FindIndex(u => u.effect == MetaEffect.WallHPPercent) : -1;
            MetaUpgrade wallUpgrade = wallIndex >= 0 ? catalog.Upgrades[wallIndex] : null;
            bool bought = legacy != null && wallIndex >= 0 && legacy.Buy(wallIndex);
            Check(bought && MetaProgress.LevelOf(wallUpgrade.id) == 1 && MetaProgress.Glory == glory - wallUpgrade.CostOf(0),
                $"Улучшение куплено за {wallUpgrade?.CostOf(0)} славы (осталось {MetaProgress.Glory})");

            // Новый забег получает купленное
            GameStats.ResetAll();
            catalog?.ApplyToNewRun();
            int expected = Mathf.RoundToInt(GameStats.BaseWallMaxHP * (1f + (wallUpgrade != null ? wallUpgrade.amountPerLevel : 0) / 100f));
            Check(GameStats.WallMaxHP == expected && expected > GameStats.BaseWallMaxHP,
                $"Новый поход начинается с улучшениями наследия (стена {GameStats.WallMaxHP} вместо {GameStats.BaseWallMaxHP})");
        }

        // Враг из пула возвращается в бой «как новый»
        private static void CheckEnemyPool()
        {
            EnemySpawner2D spawner = EnemySpawner2D.Instance;
            MethodInfo spawn = typeof(EnemySpawner2D).GetMethod("Spawn", BindingFlags.Instance | BindingFlags.NonPublic);
            GameObject pig = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Vertical/Enemy_Pig.prefab");

            var first = (GameObject)spawn.Invoke(spawner, new object[] { pig, new Vector3(0f, 6f, 0f), false, 1f });
            PigEnemy enemy = first.GetComponent<PigEnemy>();
            Vector3 scale = first.transform.localScale;
            enemy.ApplySlow(0.5f, 10f);
            enemy.ApplyDamage(1000000);
            first.transform.localScale = scale * 0.3f; // как в конце анимации смерти
            ObjectPool.Release(first);

            var second = (GameObject)spawn.Invoke(spawner, new object[] { pig, new Vector3(1f, 6f, 0f), false, 2f });
            PigEnemy reused = second.GetComponent<PigEnemy>();
            bool collidersOn = System.Array.TrueForAll(second.GetComponentsInChildren<Collider2D>(), c => c.enabled);
            Check(second == first && !reused.IsDying && reused.CurrentHP == reused.MaxHP && reused.Difficulty > 1.9f && !reused.IsSlowed
                  && second.transform.localScale == scale && collidersOn && Contains(PigEnemy.Alive, reused),
                $"Враг из пула вернулся в бой как новый (HP {reused.CurrentHP}/{reused.MaxHP}, сложность x{reused.Difficulty:0.0})");
            Object.Destroy(second);

            Check(ObjectPool.ReusedCount > 0 && ObjectPool.CreatedCount > 0,
                $"Пул объектов: создано {ObjectPool.CreatedCount}, взято повторно {ObjectPool.ReusedCount} раз");
        }

        private static bool Contains(IReadOnlyList<PigEnemy> list, PigEnemy item)
        {
            foreach (PigEnemy e in list) if (e == item) return true;
            return false;
        }

        // ---------------------------------------------------------------- Задания

        private const string QuestId = "CarlsonJumpsuit";

        // Условия появления и последствия — на пробном задании, чтобы не трогать настоящие
        private static void CheckQuestRules()
        {
            QuestCatalog quests = QuestCatalog.Instance;
            QuestDefinition quest = quests != null ? quests.Find(QuestId) : null;
            Check(quest != null && quest.lines.Count > 0, $"В каталоге заданий: {quests?.Quests.Count} (есть «{quest?.title}», {quest?.lines.Count} реплик и титров)");
            Check(quest != null && quest.town != null && quest.town.fixedMapRow >= 0,
                $"Задание привязано к сюжетному городу «{quest?.town?.displayName}» (ряд карты {quest?.town?.fixedMapRow + 1})");

            TownCatalog towns = TownCatalog.Instance;
            if (quest == null || quest.town == null || towns == null) return;

            TownDefinition home = quest.town;
            TownDefinition elsewhere = null;
            foreach (TownDefinition town in towns.Towns) if (elsewhere == null && town != null && town != home) elsewhere = town;

            var probe = ScriptableObject.CreateInstance<QuestDefinition>();
            probe.name = "SmokeTestQuest";
            probe.town = home;
            probe.requiredFlags.Add("smoke_need");
            probe.blockingFlags.Add("smoke_block");
            probe.setFlags.Add("smoke_done");
            probe.clearFlags.Add("smoke_need");

            bool withoutFlag = probe.IsAvailableIn(home);
            GameStats.SetStoryFlag("smoke_need");
            bool withFlag = probe.IsAvailableIn(home);
            bool inOtherTown = probe.IsAvailableIn(elsewhere);
            GameStats.SetStoryFlag("smoke_block");
            bool blocked = probe.IsAvailableIn(home);
            GameStats.SetStoryFlag("smoke_block", false);
            probe.Complete();
            bool repeated = probe.IsAvailableIn(home);

            Check(!withoutFlag && withFlag && !inOtherTown && !blocked && !repeated,
                "Задание появляется только в своём городе и при нужных флагах мира, второй раз не выдаётся");
            Check(GameStats.HasStoryFlag("smoke_done") && !GameStats.HasStoryFlag("smoke_need") && GameStats.IsQuestCompleted(probe.Id),
                "Выполненное задание выставляет и снимает флаги мира");

            GameStats.SetStoryFlag("smoke_done", false);
            Object.Destroy(probe);
        }

        private static void CheckQuestInVillage(VillageController village)
        {
            QuestDefinition quest = QuestCatalog.Instance != null ? QuestCatalog.Instance.Find(QuestId) : null;
            var dialogue = Object.FindAnyObjectByType<DialoguePanel>();
            if (village == null || village.Town == null || quest == null || dialogue == null)
            {
                Check(false, "Задание в деревне можно проверить");
                return;
            }

            Check(village.Town == quest.town && Contains(village.AvailableQuests, quest),
                $"В городе «{village.Town.displayName}» ждёт задание «{quest.title}»");

            int buildingIndex = village.Town.buildings.FindIndex(b => b.id == quest.buildingId);
            if (buildingIndex < 0 || buildingIndex >= village.BuildingButtons.Count)
            {
                Check(false, $"В городе есть здание задания «{quest.buildingId}»");
                return;
            }

            Transform marker = village.BuildingButtons[buildingIndex].transform.Find(VillageController.QuestMarkerName);
            Check(marker != null && marker.gameObject.activeSelf && CountQuestMarkers(village) == 1,
                $"Метка «!» горит только над зданием с заданием (отмечено зданий: {CountQuestMarkers(village)})");

            village.OpenBuilding(buildingIndex);
            int residents = village.OpenBuildingData != null ? village.OpenBuildingData.npcs.Count : -1;
            Check(village.IsBuildingOpen && village.QuestGivers.Count == 1 && village.NpcCount == residents + 1,
                $"В здании «{village.OpenBuildingData?.displayName}» появился {quest.giverName} с заданием (всего жителей: {village.NpcCount})");

            village.PlayQuestGiver(0);
            Check(dialogue.IsOpen && village.ActiveQuest == quest && dialogue.HasNarrationCard && GamePause.IsPaused,
                "Разговор с ним запускает сцену задания");

            // Листаем сцену до конца: реплики идут в окне с портретом, титры — на отдельной карточке
            int shown = 0, cards = 0, speeches = 0;
            bool layout = true;
            DialogueLine last = null;
            var speakers = new List<string>();
            int guard = 0;
            while (dialogue.IsOpen && !GameStats.IsQuestCompleted(quest.Id) && guard++ < 300)
            {
                DialogueLine line = dialogue.CurrentLine;
                if (line != null && line != last)
                {
                    last = line;
                    shown++;
                    if (line.IsNarration)
                    {
                        cards++;
                        layout &= dialogue.IsNarrationShown;
                    }
                    else
                    {
                        speeches++;
                        layout &= !dialogue.IsNarrationShown;
                        if (!speakers.Contains(line.speaker)) speakers.Add(line.speaker);
                    }
                }
                dialogue.Next();
            }

            Check(shown == quest.lines.Count && cards > 0 && speeches > 0 && layout,
                $"Сцена сыграна целиком: {speeches} реплик ({string.Join(", ", speakers)}) и {cards} титров на отдельной карточке");
            Check(GameStats.IsQuestCompleted(quest.Id) && village.LastCompletedQuest == quest && village.ActiveQuest == null, "Задание засчитано");

            bool flags = quest.setFlags.Count > 0;
            foreach (string flag in quest.setFlags) flags &= GameStats.HasStoryFlag(flag);
            Check(flags, $"Последствия записаны во флаги мира: {string.Join(", ", GameStats.StoryFlags)}");

            Check(dialogue.IsOpen && dialogue.IsNarrationShown && dialogue.CurrentLine != null && dialogue.CurrentLine.text.Contains(quest.title),
                "Показана карточка «Задание выполнено»");
            guard = 0;
            while (dialogue.IsOpen && guard++ < 10) dialogue.Next();
            Check(!dialogue.IsOpen && !GamePause.IsPaused, "После задания игра продолжается");

            Check(village.IsBuildingOpen && village.QuestGivers.Count == 0 && village.NpcCount == residents
                  && CountQuestMarkers(village) == 0 && !Contains(village.AvailableQuests, quest),
                $"{quest.giverName} пропал из здания, метка погасла, задание не повторяется");
            village.CloseBuilding();
        }

        // Инструмент сценариста: «Играть» в окне Quest Tester ставит героя сразу в город задания
        private static void CheckQuestTester()
        {
            QuestDefinition quest = QuestCatalog.Instance != null ? QuestCatalog.Instance.Find(QuestId) : null;
            if (quest == null) return;

            bool placed = QuestTestRunner.Prepare(quest);
            TownDefinition town = CampaignRun.CurrentTown;
            Check(placed && town == quest.town && quest.IsAvailableIn(town) && GameStats.Mission == RunMap.FirstLevelOfRow(CampaignRun.Map.currentRow),
                $"Quest Tester ставит героя сразу в город задания ({town?.displayName}, уровень {GameStats.Mission})");
        }

        private static bool Contains(IReadOnlyList<QuestDefinition> quests, QuestDefinition quest)
        {
            foreach (QuestDefinition item in quests) if (item == quest) return true;
            return false;
        }

        private static int CountQuestMarkers(VillageController village)
        {
            int count = 0;
            foreach (Button button in village.BuildingButtons)
            {
                Transform marker = button.transform.Find(VillageController.QuestMarkerName);
                if (marker != null && marker.gameObject.activeSelf) count++;
            }
            return count;
        }

        // ---------------------------------------------------------------- Арт: имя файла -> слот

        private static void CheckArtSlots()
        {
            ArtImporter.Slot icon = ArtImporter.Resolve("icondamage");
            ArtImporter.Slot background = ArtImporter.Resolve("Town_5");
            ArtImporter.Slot building = ArtImporter.Resolve("building_Castle");
            ArtImporter.Slot townNpc = ArtImporter.Resolve("Town_02_npc_trader");
            ArtImporter.Slot skinIcon = ArtImporter.Resolve("skin_Golden_icon");
            ArtImporter.Slot giver = ArtImporter.Resolve("quest_CarlsonJumpsuit");

            Check(icon.kind == ArtImporter.SlotKind.Catalog && icon.visual == VisualId.IconDamage
                  && background.kind == ArtImporter.SlotKind.TownBackground && background.town == "Town_05"
                  && building.kind == ArtImporter.SlotKind.Building && building.town == null && building.key == "castle"
                  && townNpc.kind == ArtImporter.SlotKind.NpcPortrait && townNpc.town == "Town_02" && townNpc.key == "trader"
                  && skinIcon.kind == ArtImporter.SlotKind.WeaponSkinIcon && skinIcon.key == "Golden"
                  && giver.kind == ArtImporter.SlotKind.QuestGiver && giver.key == "CarlsonJumpsuit",
                "Арт: имя файла определяет слот (каталог, фон города, здание, житель, скин, задание)");
            Check(!ArtImporter.Resolve("my picture").IsValid && !ArtImporter.Resolve("5").IsValid && !ArtImporter.Resolve("Town_x").IsValid
                  && !ArtImporter.Resolve("Town_03_encounter_Bandits").IsValid,
                "Арт: посторонние имена ни к чему не подключаются");
            Check(AssetDatabase.IsValidFolder(ArtImporter.InboxFolder) && System.IO.File.Exists(ArtImporter.ListPath),
                $"Есть папка для арта {ArtImporter.Root} и список слотов {ArtImporter.ListPath}");
        }

        // ---------------------------------------------------------------- Деревня

        private static void CheckVillage()
        {
            var village = Object.FindAnyObjectByType<VillageController>();
            Check(village != null && village.Town != null, $"Деревня открылась: {village?.Town?.displayName}");
            if (village == null || village.Town == null) return;

            TownCatalog towns = TownCatalog.Instance;
            Check(towns != null && towns.Towns.Count == 30, $"Городов в пуле: {towns?.Towns.Count}");
            Check(village.Town.Id == CampaignRun.CurrentTown?.Id, "Деревня — город, в котором герой стоит на карте похода");

            int clickable = 0;
            foreach (Button button in village.BuildingButtons) if (button != null && button.interactable && button.gameObject.activeInHierarchy) clickable++;
            Check(clickable >= 10, $"В деревне {clickable} кликабельных зданий");

            // Клик по зданию — окно с жителями
            village.BuildingButtons[0].onClick.Invoke();
            Check(village.IsBuildingOpen && village.NpcCount > 0,
                $"Здание «{village.OpenBuildingData?.displayName}» открылось, жителей: {village.NpcCount}");

            // Разговор с жителем
            village.Talk(0);
            var dialogue = Object.FindAnyObjectByType<DialoguePanel>();
            var spoken = new List<string>();
            int guard = 0;
            while (dialogue != null && dialogue.IsOpen && guard++ < 20)
            {
                DialogueLine line = dialogue.CurrentLine;
                if (line != null && (spoken.Count == 0 || spoken[spoken.Count - 1] != line.text)) spoken.Add(line.text);
                dialogue.Next();
            }
            string conversation = string.Join(" / ", spoken);
            Check(conversation == "УУУУУ / ААААА / Спасибо", $"Разговор с квестгивером: {conversation}");

            village.CloseBuilding();
            Check(!village.IsBuildingOpen, "Окно здания закрывается");

            // Дальше — в лобби
            GameObject.Find("ContinueButton").GetComponent<Button>().onClick.Invoke();
        }

        // ---------------------------------------------------------------- Навыки за уровень

        private static void CheckSkillPool()
        {
            ExperienceSystem experience = ExperienceSystem.Instance;
            MethodInfo addWeapon = experience.GetType().GetMethod("AddWeaponChoices", BindingFlags.Instance | BindingFlags.NonPublic);
            var choices = new List<UpgradeChoice>();
            addWeapon.Invoke(experience, new object[] { choices });

            var ids = new HashSet<string>();
            foreach (UpgradeChoice choice in choices) ids.Add(choice.Id);

            string[] expected = { "damage", "firerate", "reload", "ammo", "pierce", "explosive" };
            bool all = true;
            foreach (string id in expected) all &= ids.Contains(id);
            Check(all && choices.Count >= 8, $"Навыков на выбор: {choices.Count} ({string.Join(", ", ids)})");

            // Каждый новый навык меняет то, что обещает
            float fireDelay = GameStats.FireDelay, reload = GameStats.ReloadTime;
            int ammo = GameStats.MaxAmmo, pierce = GameStats.Pierce, explosion = GameStats.ExplosionLevel;
            foreach (UpgradeChoice choice in choices)
            {
                if (choice.Id == "firerate" || choice.Id == "reload" || choice.Id == "ammo" || choice.Id == "pierce" || choice.Id == "explosive") choice.Apply();
            }
            Check(GameStats.FireDelay < fireDelay && GameStats.ReloadTime < reload && GameStats.MaxAmmo == ammo + GameStats.AmmoStep
                  && GameStats.Pierce == pierce + 1 && GameStats.ExplosionLevel == explosion + 1,
                $"Навыки применяются: задержка {fireDelay:0.00}->{GameStats.FireDelay:0.00}, перезарядка {reload:0.0}->{GameStats.ReloadTime:0.0}, обойма {ammo}->{GameStats.MaxAmmo}, пробивание {GameStats.Pierce}, взрыв {GameStats.ExplosionLevel}");

            // Ремонт стены и сила башен
            Wall wall = Wall.ActiveInstance;
            wall.TakeDamage(30);
            int wallHp = wall.CurrentHP;
            choices.Clear();
            addWeapon.Invoke(experience, new object[] { choices });
            UpgradeChoice repair = choices.Find(c => c.Id == "repair");
            UpgradeChoice towerPower = choices.Find(c => c.Id == "towerpower");
            int towerPowerBefore = GameStats.TowerPowerLevel;
            repair?.Apply();
            towerPower?.Apply();
            Check(repair != null && wall.CurrentHP > wallHp, $"Ремонт стены: {wallHp} -> {wall.CurrentHP}");
            Check(towerPower != null && GameStats.TowerPowerLevel == towerPowerBefore + 1 && GameStats.TowerDamageMultiplier > 1f,
                $"Сила башен: уровень {towerPowerBefore} -> {GameStats.TowerPowerLevel}, урон башен x{GameStats.TowerDamageMultiplier:0.00}");

            // Пробивание и взрыв не мешают остальным проверкам
            GameStats.Pierce = 0;
            GameStats.ExplosionLevel = 0;
        }

        private static void CheckPierceAndExplosion()
        {
            // Нужны минимум 3 живых врага — доспавниваем, если поток сейчас редкий
            GameObject pig = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Vertical/Enemy_Pig.prefab");
            MethodInfo spawn = typeof(EnemySpawner2D).GetMethod("Spawn", BindingFlags.Instance | BindingFlags.NonPublic);
            for (int i = PigEnemy.AliveCount; i < 4; i++)
            {
                spawn.Invoke(EnemySpawner2D.Instance, new object[] { pig, new Vector3(-2f + i, 4f, 0f), false, 1f });
            }

            var targets = new List<PigEnemy>();
            foreach (PigEnemy enemy in PigEnemy.Alive) if (!enemy.IsBoss && !enemy.IsDying && targets.Count < 3) targets.Add(enemy);
            if (targets.Count < 3) { Check(false, "Есть враги для проверки пробивания"); return; }

            GameObject bulletPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Vertical/Bullet.prefab");
            MethodInfo trigger = typeof(BulletMovement2D).GetMethod("OnTriggerEnter2D", BindingFlags.Instance | BindingFlags.NonPublic);

            // Пробивание 1: первый враг — пуля летит дальше, второй — пуля останавливается
            GameStats.Pierce = 1;
            PlayerController2D.SpawnBullet(bulletPrefab, new Vector3(0f, 30f, 0f), Quaternion.identity, 0f, 1);
            BulletMovement2D bullet = null;
            foreach (BulletMovement2D b in Object.FindObjectsByType<BulletMovement2D>()) if (b.transform.position.y > 29f) bullet = b;
            trigger.Invoke(bullet, new object[] { targets[0].GetComponent<Collider2D>() });
            bool passedFirst = !(bool)GetField(bullet, "_hasHit");
            trigger.Invoke(bullet, new object[] { targets[1].GetComponent<Collider2D>() });
            bool stoppedSecond = (bool)GetField(bullet, "_hasHit");
            Check(passedFirst && stoppedSecond, "Пробивание: пуля прошла сквозь первого врага и остановилась на втором");
            GameStats.Pierce = 0;

            // Взрыв: сосед рядом с целью тоже получает урон
            GameStats.ExplosionLevel = 2;
            PigEnemy target = targets[2];
            PigEnemy neighbour = FindTestEnemy() == target ? targets[1] : FindTestEnemy();
            foreach (PigEnemy enemy in PigEnemy.Alive) if (enemy != target && !enemy.IsDying && !enemy.IsBoss) { neighbour = enemy; break; }
            neighbour.transform.position = target.transform.position + new Vector3(0.4f, 0f, 0f);
            int neighbourHp = neighbour.CurrentHP;

            PlayerController2D.SpawnBullet(bulletPrefab, new Vector3(0f, 31f, 0f), Quaternion.identity, 0f, 5);
            foreach (BulletMovement2D b in Object.FindObjectsByType<BulletMovement2D>()) if (b.transform.position.y > 30.5f) bullet = b;
            trigger.Invoke(bullet, new object[] { target.GetComponent<Collider2D>() });
            Check(neighbour.IsDying || neighbour.CurrentHP < neighbourHp, $"Взрывные пули задевают соседей (HP соседа {neighbourHp} -> {neighbour.CurrentHP})");
            GameStats.ExplosionLevel = 0;
        }

        // ---------------------------------------------------------------- Тело героя за прицелом

        private static void StartBodyProbe()
        {
            var player = Object.FindAnyObjectByType<PlayerController2D>();
            if (player == null) return;
            Check(player.Body != null, $"Тело героя найдено для поворота ({player.Body?.name ?? "нет"})");
            SetField(player, "_targetRotation", Quaternion.Euler(0f, 0f, 40f));
        }

        private static void CheckBodyProbe()
        {
            var player = Object.FindAnyObjectByType<PlayerController2D>();
            if (player == null || player.Body == null) return;

            var baseRotation = (Quaternion)GetField(player, "_bodyBaseRotation");
            Quaternion expected = Quaternion.Euler(0f, 0f, 40f) * baseRotation;
            float error = Quaternion.Angle(player.Body.localRotation, expected);
            Check(Mathf.Abs(player.AimOffsetAngle - 40f) < 1f && error < 1f,
                $"Тело героя поворачивается вместе с оружием (прицел {player.AimOffsetAngle:0}°, отклонение тела {error:0.0}°)");
            SetField(player, "_targetRotation", Quaternion.identity);
        }

        // ---------------------------------------------------------------- Йог-Сотот

        private static void StartYogTest()
        {
            GameStats.WallMaxHP = 100000;
            RepairWall();

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Vertical/Yog_Sothoth.prefab");
            Check(prefab != null && prefab.GetComponent<YogSothoth>() != null, "Есть префаб Йог-Сотота");

            LevelCatalog levels = LevelCatalog.Instance;
            bool onLevels = prefab != null && !levels.Get(3).miniBosses.Exists(m => m.prefab == prefab);
            for (int i = 4; onLevels && i <= GameStats.TotalLevels; i++) onLevels = levels.Get(i).miniBosses.Exists(m => m.prefab == prefab);
            Check(onLevels, "Йог-Сотот появляется на уровнях 4–30 (и не появляется на 1–3)");
            if (prefab == null) return;

            EnemySpawner2D spawner = EnemySpawner2D.Instance;
            MethodInfo spawn = spawner.GetType().GetMethod("Spawn", BindingFlags.Instance | BindingFlags.NonPublic);
            var go = (GameObject)spawn.Invoke(spawner, new object[] { prefab, new Vector3(0f, Wall.ActiveInstance.TopY + 7f, 0f), false, 1f });

            _yog = go.GetComponent<YogSothoth>();
            _yogEnemy = go.GetComponent<PigEnemy>();
            _towersBeforeYog = GameStats.TowerCount;
            _yogPhase = 0;
            _yogPhaseStart = Time.time;
        }

        // true — проверка Йога закончена
        private static bool TickYogTest()
        {
            if (_yog == null || _yogEnemy == null)
            {
                Check(false, "Йог-Сотот появился");
                return true;
            }

            float t = Time.time - _yogPhaseStart;
            Wall wall = Wall.ActiveInstance;

            switch (_yogPhase)
            {
                case 0: // идёт к стене — неуязвим
                {
                    if (t < 0.2f) return false;
                    int hp = _yogEnemy.CurrentHP;
                    _yogEnemy.ApplyDamage(100000);
                    Check(_yog.CurrentState == YogSothoth.State.Approaching && _yogEnemy.CurrentHP == hp && !_yogEnemy.IsDying,
                        $"Йог неуязвим, пока идёт к стене (HP {hp} -> {_yogEnemy.CurrentHP})");
                    NextYogPhase();
                    return false;
                }

                case 1: // остановился у стены, выросли щупальца
                {
                    if (_yog.CurrentState == YogSothoth.State.Approaching && t < 15f) return false;

                    float gap = (_yogEnemy.transform.position.y - _yogEnemy.BottomOffset) - wall.TopY;
                    Check(Mathf.Abs(gap - _yog.StopDistance) < 0.05f,
                        $"Йог остановился в 100 px от стены (зазор {gap:0.00}, нужно {_yog.StopDistance:0.00}, шёл {t:0.0} с)");
                    Check(_yog.TentaclesAlive == 3, $"Перед Йогом выросли щупальца ({_yog.TentaclesAlive})");

                    int hp = _yogEnemy.CurrentHP;
                    _yogEnemy.ApplyDamage(100000);
                    Check(_yogEnemy.CurrentHP == hp, "Пока живы щупальца, Йог неуязвим");
                    NextYogPhase();
                    return false;
                }

                case 2: // сначала разрушает башни
                {
                    if (_yog.TowersDestroyed == 0 && t < 20f) return false;
                    Check(_yog.TowersDestroyed > 0 && _yog.WallHits == 0 && GameStats.TowerCount == _towersBeforeYog - _yog.TowersDestroyed,
                        $"Йог сначала разрушает башни (разрушено {_yog.TowersDestroyed}, ударов по стене {_yog.WallHits})");

                    TowerManager towers = TowerManager.Instance;
                    foreach (GameObject tower in new List<GameObject>(towers.Towers)) towers.DestroyTower(tower.GetComponent<HelperTower>());
                    NextYogPhase();
                    return false;
                }

                case 3: // башен нет — двойной урон стене
                {
                    if (_yog.WallHits == 0 && t < 20f) return false;
                    Check(_yog.WallHits > 0 && _yog.LastWallDamage == _yogEnemy.AttackDamage * 2,
                        $"Без башен Йог бьёт стену двойным уроном ({_yog.LastWallDamage} = 2 × {_yogEnemy.AttackDamage})");

                    foreach (PigEnemy enemy in new List<PigEnemy>(PigEnemy.Alive))
                    {
                        if (enemy.name.StartsWith("Tentacle")) enemy.ApplyDamage(100000);
                    }
                    NextYogPhase();
                    return false;
                }

                case 4: // щупальца уничтожены — Йог уязвим
                {
                    if (t < 0.1f) return false;
                    int hp = _yogEnemy.CurrentHP;
                    int hits = 0;
                    while (!_yogEnemy.IsDying && hits < 20)
                    {
                        _yogEnemy.ApplyDamage(GameStats.BulletDamage);
                        hits++;
                    }
                    Check(_yog.CurrentState == YogSothoth.State.Vulnerable && _yogEnemy.IsDying && hits <= 3,
                        $"После гибели щупалец Йог умирает быстро: {hits} попадания по {GameStats.BulletDamage} (HP было {hp})");
                    return true;
                }
            }

            return true;
        }

        private static void NextYogPhase()
        {
            _yogPhase++;
            _yogPhaseStart = Time.time;
        }

        // ---------------------------------------------------------------- Щит босса

        private static void TestShield(BossAbilities abilities)
        {
            _shieldTested = true;
            Transform bar = _boss.transform.Find("ShieldBar");
            Check(abilities.ShieldHP > 0 && bar != null && bar.gameObject.activeSelf,
                $"Щит включён, у него своя полоска прочности ({abilities.ShieldHP} / {abilities.ShieldMaxHP})");

            int hpBefore = _boss.CurrentHP;
            int shieldBefore = abilities.ShieldHP;
            int small = Mathf.Max(1, shieldBefore / 4);
            _boss.ApplyDamage(small);
            Check(_boss.CurrentHP == hpBefore && abilities.ShieldHP == shieldBefore - small,
                $"Пока щит цел, урон уходит в щит (HP босса {hpBefore} -> {_boss.CurrentHP}, щит {shieldBefore} -> {abilities.ShieldHP})");

            int rest = abilities.ShieldHP;
            _boss.ApplyDamage(rest + 10);
            Check(!abilities.ShieldActive && _boss.CurrentHP == hpBefore - 10 && !bar.gameObject.activeSelf,
                $"Щит сломан — остаток урона прошёл по боссу (HP {hpBefore} -> {_boss.CurrentHP})");
        }

        // ---------------------------------------------------------------- Уровни, опыт, скины

        private static void CheckLevelsAndExperience()
        {
            LevelCatalog levels = LevelCatalog.Instance;
            bool numbered = levels != null && levels.Count == GameStats.TotalLevels;
            for (int i = 0; numbered && i < levels.Count; i++) numbered = levels.Levels[i] != null && levels.Levels[i].levelNumber == i + 1;
            Check(numbered, $"В кампании {levels?.Count} уровней, пронумерованы 1..{GameStats.TotalLevels}");

            ExperienceSystem experience = ExperienceSystem.Instance;
            int l1 = experience.XpRequiredFor(1), l2 = experience.XpRequiredFor(2), l10 = experience.XpRequiredFor(10);
            Check(l1 == 10 && l2 == 15 && l10 > 300, $"Опыт до уровня растёт по экспоненте: {l1}, {l2} … ур.10: {l10}");
            Check(ExperienceSystem.RewardFor(1, 1f) == 1 && ExperienceSystem.RewardFor(1, 4f) == 3 && ExperienceSystem.RewardFor(2, 4f) == 6,
                "Награда за врага растёт с его сложностью (x1 -> 1, x4 -> 3 опыта)");
        }

        private static void CheckWeaponSkins()
        {
            var player = Object.FindAnyObjectByType<PlayerController2D>();
            WeaponSkinCatalog catalog = WeaponSkinCatalog.Instance;
            Check(catalog != null && catalog.Skins.Count >= 3, $"Скинов оружия в каталоге: {catalog?.Skins.Count}");
            if (player == null || catalog == null) return;

            VisualSlot weapon = player.Turret.Find("Weapon")?.GetComponent<VisualSlot>();
            Sprite defaultSprite = weapon != null ? weapon.CustomSprite : null;

            GameStats.WeaponSkinId = "Blaster";
            player.ApplyWeaponSkin();
            WeaponSkin blaster = WeaponSkinCatalog.Selected;
            Check(weapon != null && blaster != null && weapon.CustomSprite == blaster.sprite && weapon.CustomColor == blaster.tint,
                $"Скин «{blaster?.displayName}» применяется к оружию ({weapon?.CurrentSource})");

            GameStats.WeaponSkinId = "Standard";
            player.ApplyWeaponSkin();
            Check(weapon != null && weapon.CustomSprite == defaultSprite, "Стандартный скин возвращает оружие как в сцене");
            GameStats.WeaponSkinId = null;
        }

        // ---------------------------------------------------------------- Оружие следует за героем

        private static void StartMountProbe()
        {
            var player = Object.FindAnyObjectByType<PlayerController2D>();
            if (player == null) return;

            Check(player.WeaponMount != null, $"Оружие прикреплено к герою (точка крепления: {player.WeaponMount?.name ?? "нет"})");

            _originalMount = player.WeaponMount;
            _originalTurretPosition = player.Turret.position;
            _mountProbe = new GameObject("TestMount").transform;
            _mountProbe.position = player.Turret.position;
            player.SetWeaponMount(_mountProbe, true);
            _mountProbe.position += new Vector3(1f, 0.5f, 0f);
        }

        private static void CheckMountProbe()
        {
            var player = Object.FindAnyObjectByType<PlayerController2D>();
            if (player == null || _mountProbe == null) return;

            Vector3 target = _originalTurretPosition + new Vector3(1f, 0.5f, 0f);
            float distance = Vector2.Distance(player.Turret.position, target);
            Check(distance < 0.05f, $"Оружие плавно догнало точку крепления (расстояние {distance:0.000})");

            player.Turret.position = _originalTurretPosition;
            player.SetWeaponMount(_originalMount, true);
            Object.Destroy(_mountProbe.gameObject);
        }

        // ---------------------------------------------------------------- Анимации

        private static void StartAnimationProbe()
        {
            _animSprites = new Sprite[6];
            for (int i = 0; i < _animSprites.Length; i++)
            {
                _animSprites[i] = Sprite.Create(new Texture2D(8, 8), new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 8);
                _animSprites[i].name = $"Frame{i}";
            }

            var go = new GameObject("TestAnimations");
            go.transform.position = new Vector3(0f, 20f, 0f);
            go.AddComponent<VisualSlot>().Configure(VisualId.Player, 0, false);
            _animProbe = go.AddComponent<CharacterAnimations>();

            SetSlot("_idle", new[] { _animSprites[0], _animSprites[1] });
            SetSlot("_attack", new[] { _animSprites[2], _animSprites[3] });
            SetSlot("_death", new[] { _animSprites[4], _animSprites[5] });

            // Animation Clip на отдельном объекте: меняет кадры спрайта (как клип, записанный художником)
            var clipGo = new GameObject("TestClip");
            clipGo.transform.position = new Vector3(0f, 25f, 0f);
            clipGo.AddComponent<VisualSlot>().Configure(VisualId.Player, 0, false);
            _clipProbe = clipGo.AddComponent<CharacterAnimations>();
            var clip = new AnimationClip { legacy = false, frameRate = 10f };
            AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), new[]
            {
                new ObjectReferenceKeyframe { time = 0f, value = _animSprites[0] },
                new ObjectReferenceKeyframe { time = 0.3f, value = _animSprites[3] },
                new ObjectReferenceKeyframe { time = 1f, value = _animSprites[3] }
            });
            ((AnimationSlot)GetField(_clipProbe, "_special")).clip = clip;
            _clipProbe.Play(CharacterAnimation.Special);

            float attack = _animProbe.Play(CharacterAnimation.Attack);
            Check(Mathf.Abs(attack - 0.2f) < 0.001f && _animProbe.GetComponent<VisualSlot>().Renderer.sprite == _animSprites[2],
                "Анимация атаки из кадров запускается");
            _animStartGameTime = Time.time;
        }

        private static void CheckAnimationProbe()
        {
            if (_animProbe == null) return;

            Sprite sprite = _animProbe.GetComponent<VisualSlot>().Renderer.sprite;
            Check(_animProbe.Current == CharacterAnimation.Idle && (sprite == _animSprites[0] || sprite == _animSprites[1]),
                $"После атаки вернулась анимация «Стоит» (кадр {sprite?.name}, прошло {Time.time - _animStartGameTime:0.00} с)");

            if (_clipProbe != null)
            {
                Sprite clipSprite = _clipProbe.GetComponent<VisualSlot>().Renderer.sprite;
                Check(clipSprite == _animSprites[3], $"Animation Clip проигрывается на объекте картинки (кадр {clipSprite?.name})");
                Object.Destroy(_clipProbe.gameObject);
            }

            // Смерть: в шаге 5 проверим, что замерла на последнем кадре
            _animProbe.Play(CharacterAnimation.Death);
            Check(_animProbe.Play(CharacterAnimation.Attack) == 0f, "После смерти другие анимации не запускаются");
        }

        private static void SetSlot(string field, Sprite[] frames)
        {
            var slot = (AnimationSlot)GetField(_animProbe, field);
            slot.frames = frames;
            slot.framesPerSecond = 10f;
        }

        private static object GetField(object target, string field)
        {
            return target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
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

            // Ручной сдвиг картинки (объект Visual) не сбрасывается и запоминается в поле «Смещение»
            go = new GameObject("TestSlotOffset");
            slot = go.AddComponent<VisualSlot>();
            slot.Configure(VisualId.EnemyPig, 10);
            go.transform.Find("Visual").localPosition = new Vector3(0f, 0.5f, 0f);
            slot.Apply();
            slot.Apply();
            Check(Vector3.Distance(go.transform.Find("Visual").localPosition, new Vector3(0f, 0.5f, 0f)) < 0.001f,
                $"Ручной сдвиг картинки сохраняется ({go.transform.Find("Visual").localPosition})");
            Object.Destroy(go);

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
