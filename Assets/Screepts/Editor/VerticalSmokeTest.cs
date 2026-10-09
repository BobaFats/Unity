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

        private static bool _retreatSeen;
        private static bool _shieldTested;
        private static int _towersAtBossSpawn;
        private static Transform _mountProbe;
        private static Transform _originalMount;
        private static Vector3 _originalTurretPosition;
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
            _failures.Clear();
            _report.Clear();
            _step = 0;
            _exceptions = 0;
            _maxAliveSeen = 0;

            _retreatSeen = false;
            _shieldTested = false;
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

                case 6: // лимит живых врагов, затем босс ур. 2
                {
                    RepairWall();
                    AutoChooseLevelUps();
                    if (Elapsed < 4) return;
                    EnemySpawner2D spawner = EnemySpawner2D.Instance;
                    int cap = (int)GetField(spawner, "_maxAliveEnemies");
                    Check(cap >= 80 && _maxAliveSeen == cap, $"Спавн упирается в лимит {cap} врагов (максимум было {_maxAliveSeen})");
                    SetField(spawner, "_spawnRate", 1000f);

                    bool onSpawnLine = true;
                    foreach (PigEnemy enemy in PigEnemy.Alive)
                    {
                        if (Mathf.Abs(enemy.transform.position.x - spawner.transform.position.x) > 3.81f) onSpawnLine = false;
                    }
                    Check(onSpawnLine, "Враги появляются в пределах линии спавна");

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

                    _towersAtBossSpawn = GameStats.TowerCount;
                    spawner.GetType().GetMethod("SpawnBoss", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(spawner, null);
                    _boss = null;
                    foreach (BossAbilities abilities in Object.FindObjectsByType<BossAbilities>()) _boss = abilities.GetComponent<PigEnemy>();
                    Check(_boss != null && _boss.GetComponent<BossAbilities>().Level == 2, "Босс ур. 2 появился");
                    BossAbility bossFlags = _boss != null ? _boss.GetComponent<BossAbilities>().Abilities : BossAbility.None;
                    Check(bossFlags == (BossAbility.Shield | BossAbility.RamWall | BossAbility.DestroyTowers), $"Способности босса из уровня 2: {bossFlags}");
                    Check(_boss != null && Mathf.Abs(_boss.transform.position.x - 2f) < 0.01f, $"Босс появился в точке BossSpawnPoint (x = {_boss?.transform.position.x:0.00})");
                    int regularLeft = 0;
                    foreach (PigEnemy enemy in PigEnemy.Alive) if (!enemy.IsBoss) regularLeft++;
                    Check(regularLeft == 0 && _boss != null && !_boss.IsDying, $"При появлении босса остальные враги убраны (осталось {regularLeft})");
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

                case 7: // щит и таран с откатом
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
                    Check(regularDuringBoss == 0, $"Пока жив босс, новые враги не появляются (на поле {regularDuringBoss})");
                    Check(abilities.TowersDestroyed > 0 && GameStats.TowerCount == _towersAtBossSpawn - abilities.TowersDestroyed,
                        $"Босс ур. 2 разрушает башни (разрушено {abilities.TowersDestroyed}, башен было {_towersAtBossSpawn}, осталось {GameStats.TowerCount})");

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
