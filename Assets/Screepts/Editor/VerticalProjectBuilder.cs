using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Tanks2D.EditorTools
{
    // Собирает вертикальную (портретную) версию игры: заглушки, каталог визуала, префабы, сцены Menu / Battle / Camp.
    // Меню: Tools > Vertical Shooter.
    public static class VerticalProjectBuilder
    {
        private const string MenuRoot = "Tools/Vertical Shooter/";

        private const string ResourcesFolder = "Assets/Resources";
        private const string PlaceholderFolder = ResourcesFolder + "/" + PlaceholderSprites.ResourceFolder;
        private const string CatalogPath = ResourcesFolder + "/" + VisualCatalog.ResourcePath + ".asset";
        private const string PrefabFolder = "Assets/Prefab/Vertical";
        private const string SceneFolder = "Assets/Scenes/Vertical";

        public const string MenuSceneName = "Menu";
        public const string BattleSceneName = "Battle";
        public const string CampSceneName = "Camp";

        private static readonly Vector2 FieldSize = new Vector2(9f, 16f);
        private static readonly Vector2 ReferenceResolution = new Vector2(1080f, 1920f);

        private static readonly Color CameraBackground = new Color(0.07f, 0.08f, 0.1f);
        private static readonly Color PanelColor = new Color(0.12f, 0.13f, 0.17f, 0.95f);
        private static readonly Color BarColor = new Color(0f, 0f, 0f, 0.55f);
        private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.75f);
        private static readonly Color ButtonColor = new Color(0.25f, 0.5f, 0.95f);
        private static readonly Color AccentButtonColor = new Color(0.95f, 0.6f, 0.15f);
        private static readonly Color DangerButtonColor = new Color(0.85f, 0.25f, 0.25f);

        private static string MenuScenePath => $"{SceneFolder}/{MenuSceneName}.unity";
        private static string BattleScenePath => $"{SceneFolder}/{BattleSceneName}.unity";
        private static string CampScenePath => $"{SceneFolder}/{CampSceneName}.unity";

        public static bool IsBuilt => File.Exists(BattleScenePath);

        private class Prefabs
        {
            public GameObject Bullet;
            public GameObject Pig;
            public GameObject Chicken;
            public GameObject Boss;
            public GameObject DamageText;
            public GameObject GoldText;
            public GameObject WallDamageText;
            public GameObject Tower;
        }

        // ---------------------------------------------------------------- Menu items

        [MenuItem(MenuRoot + "Build Vertical Game", priority = 0)]
        public static void BuildAllMenu()
        {
            string message = IsBuilt
                ? $"Префабы в {PrefabFolder} и сцены в {SceneFolder} будут пересозданы (ручные правки в них потеряются).\n\nКаталог визуала ({CatalogPath}) НЕ перезаписывается — назначенные спрайты сохранятся."
                : $"Будут созданы:\n• заглушки и каталог визуала в {ResourcesFolder}\n• префабы в {PrefabFolder}\n• сцены Menu / Battle / Camp в {SceneFolder}\n• портретные настройки Player Settings\n\nСтарые сцены и префабы не изменяются.";

            if (!EditorUtility.DisplayDialog("Vertical Shooter", message, "Собрать", "Отмена")) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            BuildAll();
        }

        [MenuItem(MenuRoot + "Refresh Visuals From Catalog", priority = 20)]
        public static void RefreshVisuals()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                bool changed = ApplyVisuals(root);
                if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
                PrefabUtility.UnloadPrefabContents(root);
            }

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                bool changed = false;
                foreach (GameObject root in scene.GetRootGameObjects()) changed |= ApplyVisuals(root);
                if (changed) EditorSceneManager.MarkSceneDirty(scene);
            }

            Debug.Log("[Vertical Shooter] Визуал обновлён из каталога. Не забудьте сохранить открытую сцену.");
        }

        [MenuItem(MenuRoot + "Select Visual Catalog", priority = 21)]
        public static void SelectCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<VisualCatalog>(CatalogPath);
            if (catalog == null)
            {
                EditorUtility.DisplayDialog("Vertical Shooter", "Каталог ещё не создан. Сначала выполните Build Vertical Game.", "OK");
                return;
            }

            Selection.activeObject = catalog;
            EditorGUIUtility.PingObject(catalog);
        }

        private static bool ApplyVisuals(GameObject root)
        {
            bool changed = false;
            foreach (VisualSlot slot in root.GetComponentsInChildren<VisualSlot>(true)) { slot.Apply(); changed = true; }
            foreach (UIVisualSlot slot in root.GetComponentsInChildren<UIVisualSlot>(true)) { slot.Apply(); changed = true; }
            return changed;
        }

        // ---------------------------------------------------------------- Build

        public static void BuildAll()
        {
            try
            {
                EditorUtility.DisplayProgressBar("Vertical Shooter", "Заглушки и каталог...", 0.1f);
                EnsureFolder(PlaceholderFolder);
                EnsureFolder(PrefabFolder);
                EnsureFolder(SceneFolder);

                CreatePlaceholderSprites();
                CreateOrUpdateCatalog();

                EditorUtility.DisplayProgressBar("Vertical Shooter", "Префабы...", 0.3f);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                Prefabs prefabs = CreatePrefabs();

                EditorUtility.DisplayProgressBar("Vertical Shooter", "Сцены...", 0.6f);
                BuildMenuScene();
                BuildCampScene();
                BuildBattleScene(prefabs);

                ConfigureBuildSettings();
                ConfigurePlayerSettings();

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            EditorSceneManager.OpenScene(BattleScenePath);
            Debug.Log("[Vertical Shooter] Готово. Запускайте сцену Menu или Battle. Арт назначается в " + CatalogPath);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        // ---------------------------------------------------------------- Placeholders & catalog

        private static void CreatePlaceholderSprites()
        {
            foreach (PlaceholderShape shape in Enum.GetValues(typeof(PlaceholderShape)))
            {
                string path = $"{PlaceholderFolder}/{shape}.png";

                Texture2D texture = PlaceholderSprites.CreateTexture(shape);
                File.WriteAllBytes(path, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);

                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = PlaceholderSprites.TextureSize;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.Uncompressed;

                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                settings.spriteAlignment = (int)SpriteAlignment.Center;
                importer.SetTextureSettings(settings);

                importer.SaveAndReimport();
            }

            PlaceholderSprites.ClearCache();
        }

        private static void CreateOrUpdateCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<VisualCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<VisualCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            Add(catalog, VisualId.Background, PlaceholderShape.Rectangle, new Color(0.2f, 0.32f, 0.2f), FieldSize, "");
            Add(catalog, VisualId.Wall, PlaceholderShape.Rectangle, new Color(0.62f, 0.45f, 0.3f), new Vector2(FieldSize.x, 0.6f), "СТЕНА");
            Add(catalog, VisualId.Player, PlaceholderShape.Circle, new Color(0.3f, 0.6f, 1f), new Vector2(1.2f, 1.2f), "ГЕРОЙ");
            Add(catalog, VisualId.Weapon, PlaceholderShape.Rectangle, new Color(0.75f, 0.78f, 0.82f), new Vector2(0.28f, 1.1f), "");
            Add(catalog, VisualId.Bullet, PlaceholderShape.Circle, new Color(1f, 0.92f, 0.35f), new Vector2(0.22f, 0.22f), "");
            Add(catalog, VisualId.EnemyPig, PlaceholderShape.Rectangle, new Color(1f, 0.6f, 0.72f), new Vector2(0.9f, 0.9f), "СВИН");
            Add(catalog, VisualId.EnemyChicken, PlaceholderShape.Triangle, new Color(0.96f, 0.94f, 0.8f), new Vector2(0.8f, 0.8f), "КУРА");
            Add(catalog, VisualId.Boss, PlaceholderShape.Diamond, new Color(0.85f, 0.2f, 0.3f), new Vector2(2.4f, 2.4f), "БОСС");
            Add(catalog, VisualId.Coin, PlaceholderShape.Circle, new Color(1f, 0.82f, 0.1f), new Vector2(0.5f, 0.5f), "$");
            Add(catalog, VisualId.IconDamage, PlaceholderShape.Diamond, new Color(1f, 0.42f, 0.24f), Vector2.one, "УРН");
            Add(catalog, VisualId.IconFireRate, PlaceholderShape.Triangle, new Color(0.24f, 0.78f, 1f), Vector2.one, "СКР");
            Add(catalog, VisualId.IconReload, PlaceholderShape.Circle, new Color(1f, 0.82f, 0.24f), Vector2.one, "ПЕР");
            Add(catalog, VisualId.IconAmmo, PlaceholderShape.Rectangle, new Color(0.36f, 0.88f, 0.42f), Vector2.one, "ОБМ");
            Add(catalog, VisualId.IconWallHP, PlaceholderShape.Rectangle, new Color(0.78f, 0.63f, 0.48f), Vector2.one, "СТН");
            Add(catalog, VisualId.Tower, PlaceholderShape.Rectangle, new Color(0.3f, 0.8f, 0.75f), new Vector2(0.9f, 0.9f), "БАШНЯ");
            Add(catalog, VisualId.IconTower, PlaceholderShape.Rectangle, new Color(0.3f, 0.8f, 0.75f), Vector2.one, "БШН");
            Add(catalog, VisualId.IconMultiShot, PlaceholderShape.Triangle, new Color(0.72f, 0.55f, 1f), Vector2.one, "x2");
            Add(catalog, VisualId.IconSpread, PlaceholderShape.Diamond, new Color(0.55f, 0.88f, 1f), Vector2.one, "<>");

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            VisualCatalog.SetInstance(catalog);
        }

        private static void Add(VisualCatalog catalog, VisualId id, PlaceholderShape shape, Color color, Vector2 size, string label)
        {
            catalog.AddIfMissing(new VisualEntry { id = id, shape = shape, color = color, size = size, label = label });
        }

        // ---------------------------------------------------------------- Prefabs

        private static Prefabs CreatePrefabs()
        {
            var prefabs = new Prefabs
            {
                DamageText = CreateFloatingTextPrefab("FloatingText_Damage", Color.white, 4f),
                GoldText = CreateFloatingTextPrefab("FloatingText_Gold", new Color(1f, 0.84f, 0f), 4.5f),
                WallDamageText = CreateFloatingTextPrefab("FloatingText_Wall", new Color(0.75f, 0.35f, 1f), 4.5f),
                Bullet = CreateBulletPrefab()
            };

            prefabs.Pig = CreateEnemyPrefab("Enemy_Pig", VisualId.EnemyPig, 30, 2f, 10, 1, prefabs);
            prefabs.Chicken = CreateEnemyPrefab("Enemy_Chicken", VisualId.EnemyChicken, 15, 4f, 5, 2, prefabs);
            prefabs.Boss = CreateEnemyPrefab("Boss_Pig", VisualId.Boss, 300, 0.5f, 50, 15, prefabs);
            prefabs.Tower = CreateTowerPrefab(prefabs.Bullet);
            return prefabs;
        }

        private static GameObject CreateFloatingTextPrefab(string name, Color color, float fontSize)
        {
            var go = new GameObject(name);
            TextMeshPro text = go.AddComponent<TextMeshPro>();
            text.text = "0";
            text.fontSize = fontSize;
            text.fontStyle = FontStyles.Bold;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.rectTransform.sizeDelta = new Vector2(3f, 1f);
            text.sortingOrder = 500;

            go.AddComponent<DamageText>();
            return SavePrefab(go, name);
        }

        private static GameObject CreateBulletPrefab()
        {
            var go = new GameObject("Bullet");
            go.AddComponent<VisualSlot>().Configure(VisualId.Bullet, 30, false);

            Rigidbody2D body = go.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            CircleCollider2D collider = go.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = VisualCatalog.Resolve(VisualId.Bullet).size.x * 0.5f;

            go.AddComponent<BulletMovement2D>();
            return SavePrefab(go, "Bullet");
        }

        private static GameObject CreateEnemyPrefab(string name, VisualId visualId, int maxHP, float speed, int attackDamage, int xpReward, Prefabs prefabs)
        {
            var go = new GameObject(name);
            VisualSlot visual = go.AddComponent<VisualSlot>();
            visual.Configure(visualId, 10);

            Vector2 size = VisualCatalog.Resolve(visualId).size;

            BoxCollider2D collider = go.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = size;

            // Кинематическое тело: враг двигается через transform, но участвует в триггерах
            Rigidbody2D body = go.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;

            var barGo = new GameObject("HealthBar");
            barGo.transform.SetParent(go.transform, false);
            barGo.transform.localPosition = new Vector3(0f, size.y * 0.5f + 0.18f, 0f);
            EnemyHealthBar2D healthBar = barGo.AddComponent<EnemyHealthBar2D>();
            healthBar.Build(PlaceholderSprites.Get(PlaceholderShape.Rectangle), new Vector2(Mathf.Max(0.8f, size.x), 0.14f), 40);

            PigEnemy enemy = go.AddComponent<PigEnemy>();
            Wire(enemy,
                ("maxHP", maxHP),
                ("speed", speed),
                ("attackDamage", attackDamage),
                ("attackRate", 1.5f),
                ("visual", visual),
                ("healthBar", healthBar),
                ("damageTextPrefab", prefabs.DamageText),
                ("goldTextPrefab", prefabs.GoldText),
                ("xpReward", xpReward));

            return SavePrefab(go, name);
        }

        private static GameObject CreateTowerPrefab(GameObject bulletPrefab)
        {
            var go = new GameObject("Tower");
            go.AddComponent<VisualSlot>().Configure(VisualId.Tower, 16);

            // Ствол — тот же визуал, что у героя, но поменьше
            var turret = new GameObject("Turret").transform;
            turret.SetParent(go.transform, false);
            turret.localScale = new Vector3(0.7f, 0.7f, 1f);

            Vector2 weaponSize = VisualCatalog.Resolve(VisualId.Weapon).size;
            var weapon = new GameObject("Weapon");
            weapon.transform.SetParent(turret, false);
            weapon.transform.localPosition = new Vector3(0f, weaponSize.y * 0.5f, 0f);
            weapon.AddComponent<VisualSlot>().Configure(VisualId.Weapon, 18, false);

            var firePoint = new GameObject("FirePoint").transform;
            firePoint.SetParent(turret, false);
            firePoint.localPosition = new Vector3(0f, weaponSize.y + 0.1f, 0f);

            HelperTower tower = go.AddComponent<HelperTower>();
            Wire(tower,
                ("_turret", turret),
                ("_firePoint", firePoint),
                ("_bulletPrefab", bulletPrefab),
                ("_fireDelay", 1f),
                ("_bulletSpeed", 12f),
                ("_damageMultiplier", 0.5f));

            return SavePrefab(go, "Tower");
        }

        private static GameObject SavePrefab(GameObject go, string name)
        {
            string path = $"{PrefabFolder}/{name}.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        // ---------------------------------------------------------------- Scenes

        private static void BuildMenuScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCamera(null);
            CreateEventSystem();

            RectTransform area = CreateCanvas("Canvas");

            TextMeshProUGUI title = CreateText(area, "Title", "MONSTER\nSHOOTER", 130, TextAlignmentOptions.Center);
            SetAnchors(title.rectTransform, new Vector2(0f, 0.62f), new Vector2(1f, 0.9f));

            RectTransform buttons = CreateVerticalGroup(area, "Buttons", 40);
            SetAnchors(buttons, new Vector2(0.15f, 0.2f), new Vector2(0.85f, 0.55f));

            Button newGame = CreateButton(buttons, "NewGameButton", "Новая игра", AccentButtonColor, 180);
            Button loadGame = CreateButton(buttons, "LoadGameButton", "Продолжить", ButtonColor, 160);
            Button settings = CreateButton(buttons, "SettingsButton", "Настройки", ButtonColor, 160);

            var manager = new GameObject("MainMenuManager").AddComponent<MainMenuController>();
            Wire(manager,
                ("newGameButton", newGame),
                ("loadGameButton", loadGame),
                ("settingsButton", settings),
                ("gameplaySceneName", BattleSceneName));

            EditorSceneManager.SaveScene(scene, MenuScenePath);
        }

        private static void BuildCampScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCamera(null);
            CreateEventSystem();

            RectTransform area = CreateCanvas("Canvas");

            TextMeshProUGUI title = CreateText(area, "Title", "ЛАГЕРЬ", 120, TextAlignmentOptions.Center);
            SetAnchors(title.rectTransform, new Vector2(0f, 0.75f), new Vector2(1f, 0.9f));

            TextMeshProUGUI info = CreateText(area, "Info", "Босс повержен!\nУлучшения и золото сохранены.", 56, TextAlignmentOptions.Center);
            SetAnchors(info.rectTransform, new Vector2(0.05f, 0.58f), new Vector2(0.95f, 0.74f));

            RectTransform goldRow = CreateGoldCounter(area, "Золото: {0}");
            SetAnchors(goldRow, new Vector2(0.2f, 0.5f), new Vector2(0.8f, 0.56f));

            RectTransform buttons = CreateVerticalGroup(area, "Buttons", 40);
            SetAnchors(buttons, new Vector2(0.15f, 0.2f), new Vector2(0.85f, 0.42f));

            Button nextBattle = CreateButton(buttons, "NextBattleButton", "Следующий бой", AccentButtonColor, 180);
            Wire(nextBattle.gameObject.AddComponent<SceneLoadButton>(), ("_sceneName", BattleSceneName), ("_resetProgress", false));

            Button menu = CreateButton(buttons, "MenuButton", "Главное меню", ButtonColor, 160);
            Wire(menu.gameObject.AddComponent<SceneLoadButton>(), ("_sceneName", MenuSceneName), ("_resetProgress", false));

            EditorSceneManager.SaveScene(scene, CampScenePath);
        }

        private static void BuildBattleScene(Prefabs prefabs)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // --- Мир
            var fieldGo = new GameObject("PlayField");
            PlayField field = fieldGo.AddComponent<PlayField>();
            Wire(field, ("_size", FieldSize));

            CreateCamera(field);
            CreateGlobalLight();
            CreateEventSystem();

            var background = new GameObject("Background");
            background.transform.SetParent(fieldGo.transform, false);
            background.AddComponent<VisualSlot>().Configure(VisualId.Background, -100, false);

            float bottom = -FieldSize.y * 0.5f;

            var wallGo = new GameObject("Wall");
            wallGo.transform.position = new Vector3(0f, bottom + 3.4f, 0f);
            VisualSlot wallVisual = wallGo.AddComponent<VisualSlot>();
            wallVisual.Configure(VisualId.Wall, 0);
            Wall wall = wallGo.AddComponent<Wall>();

            PlayerController2D player = CreatePlayer(new Vector3(0f, bottom + 1.9f, 0f), prefabs.Bullet);

            var spawnerGo = new GameObject("EnemySpawner");
            spawnerGo.transform.position = new Vector3(0f, -bottom + 1f, 0f);
            EnemySpawner2D spawner = spawnerGo.AddComponent<EnemySpawner2D>();
            AudioSource music = spawnerGo.AddComponent<AudioSource>();
            music.playOnAwake = false;
            music.loop = true;
            music.volume = 0.6f;

            Wire(spawner,
                ("_wall", wall),
                ("_spawnRate", 2f),
                ("bossPrefab", prefabs.Boss),
                ("killsNeededForBoss", 60),
                ("_doublingPeriod", 60f),
                ("_maxAliveEnemies", 50),
                ("_spawnDuringBoss", true),
                ("bossGoldReward", 100),
                ("campSceneName", CampSceneName),
                ("delayBeforeCamp", 3f),
                ("musicAudioSource", music),
                ("normalWaveMusic", AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Music/Музыка игра.mp3")),
                ("bossMusic", AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Music/Босс.mp3")));

            SetEnemyConfigs(spawner,
                ("Свинья", prefabs.Pig, 70, 1),
                ("Курица", prefabs.Chicken, 30, 3));

            // --- Интерфейс
            RectTransform area = CreateCanvas("HUD");
            BuildTopBar(area, wall);
            BuildBottomBar(area, player);
            BuildShop(area);
            LevelUpPanel levelUpPanel = BuildLevelUp(area);
            BuildGameOver(area, wall);

            // --- Опыт и башни
            var towersGo = new GameObject("Towers");
            TowerManager towers = towersGo.AddComponent<TowerManager>();
            Wire(towers, ("_towerPrefab", prefabs.Tower), ("_wall", wall));

            var experienceGo = new GameObject("ExperienceSystem");
            ExperienceSystem experience = experienceGo.AddComponent<ExperienceSystem>();
            Wire(experience, ("_panel", levelUpPanel), ("_towers", towers));

            Wire(wall,
                ("_visual", wallVisual),
                ("damageTextPrefab", prefabs.WallDamageText),
                ("hpGradient", CreateHpGradient()));

            EditorSceneManager.SaveScene(scene, BattleScenePath);
        }

        private static PlayerController2D CreatePlayer(Vector3 position, GameObject bulletPrefab)
        {
            var playerGo = new GameObject("Player");
            playerGo.transform.position = position;
            playerGo.AddComponent<VisualSlot>().Configure(VisualId.Player, 20);

            // Ствол вращается отдельно, чтобы подпись "ГЕРОЙ" не крутилась вместе с ним
            var turret = new GameObject("Turret").transform;
            turret.SetParent(playerGo.transform, false);

            Vector2 weaponSize = VisualCatalog.Resolve(VisualId.Weapon).size;
            var weapon = new GameObject("Weapon");
            weapon.transform.SetParent(turret, false);
            weapon.transform.localPosition = new Vector3(0f, weaponSize.y * 0.5f, 0f);
            weapon.AddComponent<VisualSlot>().Configure(VisualId.Weapon, 19, false);

            var firePoint = new GameObject("FirePoint").transform;
            firePoint.SetParent(turret, false);
            firePoint.localPosition = new Vector3(0f, weaponSize.y + 0.1f, 0f);

            PlayerController2D controller = playerGo.AddComponent<PlayerController2D>();
            Wire(controller,
                ("_turret", turret),
                ("_firePoint", firePoint),
                ("_bulletPrefab", bulletPrefab),
                ("_bulletSpeed", 14f),
                ("_minAimAngle", 10f));

            return controller;
        }

        private static void SetEnemyConfigs(EnemySpawner2D spawner, params (string name, GameObject prefab, int chance, int gold)[] configs)
        {
            var so = new SerializedObject(spawner);
            SerializedProperty array = so.FindProperty("_enemiesConfigs");
            array.arraySize = configs.Length;

            for (int i = 0; i < configs.Length; i++)
            {
                SerializedProperty element = array.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("enemyName").stringValue = configs[i].name;
                element.FindPropertyRelative("enemyPrefab").objectReferenceValue = configs[i].prefab;
                element.FindPropertyRelative("spawnChance").intValue = configs[i].chance;
                element.FindPropertyRelative("goldReward").intValue = configs[i].gold;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Gradient CreateHpGradient()
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.9f, 0.2f, 0.2f), 0f),
                    new GradientColorKey(new Color(1f, 0.8f, 0.2f), 0.5f),
                    new GradientColorKey(new Color(0.3f, 0.85f, 0.35f), 1f)
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return gradient;
        }

        // ---------------------------------------------------------------- Battle UI

        private static void BuildTopBar(RectTransform area, Wall wall)
        {
            RectTransform bar = CreateRect("TopBar", area);
            SetAnchors(bar, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
            bar.sizeDelta = new Vector2(0f, 310f);
            AddImage(bar, BarColor);

            // Строка 1: золото | прогресс | магазин
            RectTransform gold = CreateGoldCounter(bar, "{0}");
            SetAnchors(gold, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            gold.anchoredPosition = new Vector2(30f, -20f);
            gold.sizeDelta = new Vector2(320f, 100f);

            TextMeshProUGUI progress = CreateText(bar, "WaveProgress", "До босса: 0 / 10", 44, TextAlignmentOptions.Center);
            SetAnchors(progress.rectTransform, new Vector2(0.3f, 1f), new Vector2(0.7f, 1f), new Vector2(0.5f, 1f));
            progress.rectTransform.anchoredPosition = new Vector2(0f, -20f);
            progress.rectTransform.sizeDelta = new Vector2(0f, 100f);
            progress.gameObject.AddComponent<WaveProgressUI>();
            Wire(progress.GetComponent<WaveProgressUI>(), ("_text", progress));

            Button shopButton = CreateButton(bar, "OpenShopButton", "Магазин", AccentButtonColor, 100);
            RectTransform shopRect = (RectTransform)shopButton.transform;
            SetAnchors(shopRect, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f));
            shopRect.anchoredPosition = new Vector2(-30f, -20f);
            shopRect.sizeDelta = new Vector2(280f, 100f);

            // Строка 2: HP стены
            Slider wallSlider = CreateSlider(bar, "WallHPSlider", new Color(0.3f, 0.85f, 0.35f));
            RectTransform sliderRect = (RectTransform)wallSlider.transform;
            SetAnchors(sliderRect, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f));
            sliderRect.offsetMin = new Vector2(30f, 100f);
            sliderRect.offsetMax = new Vector2(-30f, 160f);

            TextMeshProUGUI wallText = CreateText(sliderRect, "WallHpText", "100 / 100", 40, TextAlignmentOptions.Center);
            Stretch(wallText.rectTransform);

            Wire(wall, ("wallHPSlider", wallSlider), ("wallHPText", wallText));

            // Строка 3: опыт
            Slider xpSlider = CreateSlider(bar, "XpSlider", new Color(0.72f, 0.55f, 1f));
            RectTransform xpRect = (RectTransform)xpSlider.transform;
            SetAnchors(xpRect, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f));
            xpRect.offsetMin = new Vector2(30f, 25f);
            xpRect.offsetMax = new Vector2(-30f, 80f);
            xpSlider.value = 0f;

            TextMeshProUGUI xpText = CreateText(xpRect, "XpText", "Ур. 1", 36, TextAlignmentOptions.Center);
            Stretch(xpText.rectTransform);

            Wire(xpRect.gameObject.AddComponent<XpBarUI>(), ("_slider", xpSlider), ("_text", xpText));

            // Ссылку на кнопку магазина ShopController получит в BuildShop
            shopButton.gameObject.name = "OpenShopButton";
        }

        private static void BuildBottomBar(RectTransform area, PlayerController2D player)
        {
            RectTransform bar = CreateRect("BottomBar", area);
            SetAnchors(bar, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f));
            bar.sizeDelta = new Vector2(0f, 150f);

            TextMeshProUGUI ammo = CreateText(bar, "AmmoText", "Патроны: 15 / 15", 44, TextAlignmentOptions.MidlineLeft);
            SetAnchors(ammo.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f));
            ammo.rectTransform.anchoredPosition = new Vector2(30f, 20f);
            ammo.rectTransform.sizeDelta = new Vector2(360f, -60f);

            Slider reloadSlider = CreateSlider(bar, "ReloadSlider", new Color(1f, 0.82f, 0.24f));
            RectTransform reloadRect = (RectTransform)reloadSlider.transform;
            SetAnchors(reloadRect, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f));
            reloadRect.anchoredPosition = new Vector2(30f, 20f);
            reloadRect.sizeDelta = new Vector2(320f, 22f);

            Button reloadButton = CreateButton(bar, "ReloadButton", "R", ButtonColor, 56);
            RectTransform buttonRect = (RectTransform)reloadButton.transform;
            SetAnchors(buttonRect, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));
            buttonRect.anchoredPosition = new Vector2(-30f, 0f);
            buttonRect.sizeDelta = new Vector2(120f, 120f);
            CreateIcon(buttonRect, "Icon", VisualId.IconReload, new Vector2(80f, 80f), false);
            reloadButton.GetComponentInChildren<TextMeshProUGUI>().gameObject.SetActive(false);

            AmmoDisplay display = bar.gameObject.AddComponent<AmmoDisplay>();
            Wire(display,
                ("ammoText", ammo),
                ("reloadSlider", reloadSlider),
                ("reloadButton", reloadButton),
                ("playerController", player));
        }

        private static void BuildShop(RectTransform area)
        {
            RectTransform panel = CreateRect("ShopPanel", area);
            Stretch(panel);
            AddImage(panel, DimColor);

            RectTransform window = CreateRect("Window", panel);
            SetAnchors(window, new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.92f));
            AddImage(window, PanelColor);

            VerticalLayoutGroup layout = window.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(40, 40, 40, 40);
            layout.spacing = 22f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            AddLayoutHeight(CreateText(window, "Title", "МАГАЗИН", 80, TextAlignmentOptions.Center).gameObject, 100);
            AddLayoutHeight(CreateGoldCounter(window, "Золото: {0}").gameObject, 80);

            TextMeshProUGUI stats = CreateText(window, "StatsText", "", 38, TextAlignmentOptions.TopLeft);
            AddLayoutHeight(stats.gameObject, 250);
            Wire(stats.gameObject.AddComponent<StatsDisplay>(), ("statsText", stats));

            CreateUpgradeRow(window, UpgradeType.Damage, VisualId.IconDamage, "Урон пули", 10, 1.5f, 1f, 0f);
            CreateUpgradeRow(window, UpgradeType.FireRate, VisualId.IconFireRate, "Скорострельность", 25, 1.6f, -0.04f, 0.1f);
            CreateUpgradeRow(window, UpgradeType.ReloadTime, VisualId.IconReload, "Перезарядка", 10, 1.5f, -0.2f, 0.4f);
            CreateUpgradeRow(window, UpgradeType.MaxAmmo, VisualId.IconAmmo, "Размер обоймы", 30, 1.7f, 5f, 50f);
            CreateUpgradeRow(window, UpgradeType.WallHP, VisualId.IconWallHP, "Прочность стены", 15, 1.6f, 50f, 500f);

            var spacer = CreateRect("Spacer", window).gameObject;
            spacer.AddComponent<LayoutElement>().flexibleHeight = 1f;

            Button close = CreateButton(window, "CloseButton", "Закрыть", DangerButtonColor, 56);
            AddLayoutHeight(close.gameObject, 130);

            var shopGo = new GameObject("ShopController");
            ShopController shop = shopGo.AddComponent<ShopController>();
            Button open = area.Find("TopBar/OpenShopButton").GetComponent<Button>();
            Wire(shop, ("shopPanel", panel.gameObject), ("openButton", open), ("closeButton", close));

            panel.gameObject.SetActive(false);
        }

        private static void CreateUpgradeRow(RectTransform parent, UpgradeType type, VisualId icon, string title,
            int basePrice, float multiplier, float step, float limit)
        {
            RectTransform row = CreateRect($"Upgrade_{type}", parent);
            AddLayoutHeight(row.gameObject, 140);
            AddImage(row, new Color(1f, 1f, 1f, 0.06f));

            HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(20, 20, 15, 15);
            layout.spacing = 24f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            Image iconImage = CreateIcon(row, "Icon", icon, new Vector2(110f, 110f), true);
            LayoutElement iconLayout = iconImage.gameObject.AddComponent<LayoutElement>();
            iconLayout.preferredWidth = 110f;

            TextMeshProUGUI info = CreateText(row, "Info", title, 38, TextAlignmentOptions.MidlineLeft);
            info.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            Button buy = CreateButton(row, "BuyButton", "Купить", AccentButtonColor, 44);
            buy.gameObject.AddComponent<LayoutElement>().preferredWidth = 230f;

            UpgradeButton upgrade = row.gameObject.AddComponent<UpgradeButton>();
            Wire(upgrade,
                ("upgradeInfoText", info),
                ("buyButton", buy),
                ("upgradeType", type),
                ("upgradeName", title),
                ("basePrice", basePrice),
                ("priceMultiplier", multiplier),
                ("stepPerLevel", step),
                ("limit", limit));
        }

        private static LevelUpPanel BuildLevelUp(RectTransform area)
        {
            // Компонент на всегда активном объекте, панель — дочерняя и скрыта
            RectTransform holder = CreateRect("LevelUp", area);
            Stretch(holder);
            LevelUpPanel levelUp = holder.gameObject.AddComponent<LevelUpPanel>();

            RectTransform panel = CreateRect("Panel", holder);
            Stretch(panel);
            AddImage(panel, DimColor);

            RectTransform window = CreateRect("Window", panel);
            SetAnchors(window, new Vector2(0.06f, 0.2f), new Vector2(0.94f, 0.8f));
            AddImage(window, PanelColor);

            VerticalLayoutGroup layout = window.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(40, 40, 40, 40);
            layout.spacing = 24f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            TextMeshProUGUI title = CreateText(window, "Title", "НОВЫЙ УРОВЕНЬ!", 72, TextAlignmentOptions.Center);
            AddLayoutHeight(title.gameObject, 170);

            var options = new (LevelUpOption option, VisualId icon)[]
            {
                (LevelUpOption.Tower, VisualId.IconTower),
                (LevelUpOption.Damage, VisualId.IconDamage),
                (LevelUpOption.MultiShot, VisualId.IconMultiShot),
                (LevelUpOption.Spread, VisualId.IconSpread)
            };

            var so = new SerializedObject(levelUp);
            SerializedProperty array = so.FindProperty("_options");
            array.arraySize = options.Length;

            for (int i = 0; i < options.Length; i++)
            {
                Button button = CreateButton(window, $"Option_{options[i].option}", options[i].option.ToString(), new Color(0.22f, 0.24f, 0.32f), 44);
                AddLayoutHeight(button.gameObject, 170);

                // Иконка слева, текст справа
                TextMeshProUGUI text = button.GetComponentInChildren<TextMeshProUGUI>();
                text.alignment = TextAlignmentOptions.MidlineLeft;
                text.rectTransform.offsetMin = new Vector2(190f, 10f);

                Image icon = CreateIcon(button.transform, "Icon", options[i].icon, new Vector2(130f, 130f), true);
                SetAnchors(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
                icon.rectTransform.anchoredPosition = new Vector2(30f, 0f);
                icon.rectTransform.sizeDelta = new Vector2(130f, 130f);

                SerializedProperty element = array.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("option").intValue = (int)options[i].option;
                element.FindPropertyRelative("button").objectReferenceValue = button;
                element.FindPropertyRelative("text").objectReferenceValue = text;
            }

            so.FindProperty("_root").objectReferenceValue = panel.gameObject;
            so.FindProperty("_title").objectReferenceValue = title;
            so.ApplyModifiedPropertiesWithoutUndo();

            panel.gameObject.SetActive(false);
            return levelUp;
        }

        private static void BuildGameOver(RectTransform area, Wall wall)
        {
            RectTransform panel = CreateRect("GameOverPanel", area);
            Stretch(panel);
            AddImage(panel, DimColor);

            TextMeshProUGUI title = CreateText(panel, "GameOverText", "СТЕНА РАЗРУШЕНА", 90, TextAlignmentOptions.Center);
            SetAnchors(title.rectTransform, new Vector2(0.05f, 0.58f), new Vector2(0.95f, 0.72f));

            RectTransform buttons = CreateVerticalGroup(panel, "Buttons", 40);
            SetAnchors(buttons, new Vector2(0.15f, 0.3f), new Vector2(0.85f, 0.52f));

            Button restart = CreateButton(buttons, "RestartButton", "Начать заново", AccentButtonColor, 64);
            Button menu = CreateButton(buttons, "MenuButton", "Главное меню", ButtonColor, 64);
            Wire(menu.gameObject.AddComponent<SceneLoadButton>(), ("_sceneName", MenuSceneName), ("_resetProgress", true));

            Wire(wall, ("gameOverPanel", panel.gameObject), ("restartButton", restart));

            panel.gameObject.SetActive(false);
        }

        // ---------------------------------------------------------------- Scene helpers

        private static Camera CreateCamera(PlayField field)
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            go.transform.position = new Vector3(0f, 0f, -10f);

            Camera camera = go.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = FieldSize.y * 0.5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = CameraBackground;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 100f;

            go.AddComponent<AudioListener>();
            if (!go.TryGetComponent(out UniversalAdditionalCameraData _)) go.AddComponent<UniversalAdditionalCameraData>();

            CameraFitter fitter = go.AddComponent<CameraFitter>();
            Wire(fitter, ("_field", field), ("_fallbackSize", FieldSize));
            return camera;
        }

        private static void CreateGlobalLight()
        {
            var go = new GameObject("Global Light 2D");
            Light2D light = go.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.intensity = 1f;
            light.color = Color.white;
        }

        private static void CreateEventSystem()
        {
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }

        // Canvas -> SafeArea -> PlayArea (колонка 9:16). Возвращает PlayArea — корень для всего UI сцены.
        private static RectTransform CreateCanvas(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");

            Canvas canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            Wire(go.AddComponent<AdaptiveCanvasScaler>(), ("_referenceResolution", ReferenceResolution));

            go.AddComponent<GraphicRaycaster>();

            RectTransform safeArea = CreateRect("SafeArea", go.transform);
            Stretch(safeArea);
            safeArea.gameObject.AddComponent<SafeAreaFitter>();

            RectTransform playArea = CreateRect("PlayArea", safeArea);
            SetAnchors(playArea, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f));
            playArea.sizeDelta = new Vector2(ReferenceResolution.x, 0f);
            Wire(playArea.gameObject.AddComponent<PlayAreaFitter>(), ("_aspect", FieldSize.x / FieldSize.y));

            return playArea;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max, Vector2? pivot = null)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void Stretch(RectTransform rect)
        {
            SetAnchors(rect, Vector2.zero, Vector2.one);
        }

        private static Image AddImage(RectTransform rect, Color color)
        {
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            image.type = Image.Type.Sliced;
            image.color = color;
            return image;
        }

        private static void AddLayoutHeight(GameObject go, float height)
        {
            LayoutElement element = go.GetComponent<LayoutElement>();
            if (element == null) element = go.AddComponent<LayoutElement>();
            element.preferredHeight = height;
            element.minHeight = height;
        }

        private static TextMeshProUGUI CreateText(Transform parent, string name, string text, float fontSize, TextAlignmentOptions alignment)
        {
            RectTransform rect = CreateRect(name, parent);
            TextMeshProUGUI tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = Mathf.Min(18f, fontSize);
            tmp.fontSizeMax = fontSize;
            tmp.alignment = alignment;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static Button CreateButton(Transform parent, string name, string label, Color color, float fontSize)
        {
            RectTransform rect = CreateRect(name, parent);
            Image image = AddImage(rect, color);

            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.6f);
            button.colors = colors;

            TextMeshProUGUI text = CreateText(rect, "Text", label, fontSize, TextAlignmentOptions.Center);
            Stretch(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(10f, 5f);
            text.rectTransform.offsetMax = new Vector2(-10f, -5f);

            return button;
        }

        private static Slider CreateSlider(Transform parent, string name, Color fillColor)
        {
            RectTransform rect = CreateRect(name, parent);

            RectTransform background = CreateRect("Background", rect);
            Stretch(background);
            AddImage(background, new Color(0f, 0f, 0f, 0.6f));

            RectTransform fillArea = CreateRect("Fill Area", rect);
            Stretch(fillArea);

            RectTransform fill = CreateRect("Fill", fillArea);
            Stretch(fill);
            Image fillImage = AddImage(fill, fillColor);

            Slider slider = rect.gameObject.AddComponent<Slider>();
            slider.fillRect = fill;
            slider.targetGraphic = fillImage;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;
            slider.interactable = false;
            slider.transition = Selectable.Transition.None;

            Navigation navigation = slider.navigation;
            navigation.mode = Navigation.Mode.None;
            slider.navigation = navigation;

            return slider;
        }

        private static Image CreateIcon(Transform parent, string name, VisualId id, Vector2 size, bool showLabel)
        {
            RectTransform rect = CreateRect(name, parent);
            rect.sizeDelta = size;
            Image image = rect.gameObject.AddComponent<Image>();
            rect.gameObject.AddComponent<UIVisualSlot>().Configure(id, showLabel);
            return image;
        }

        private static RectTransform CreateGoldCounter(Transform parent, string format)
        {
            RectTransform row = CreateRect("Gold", parent);

            HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            Image icon = CreateIcon(row, "CoinIcon", VisualId.Coin, new Vector2(70f, 70f), true);
            LayoutElement iconLayout = icon.gameObject.AddComponent<LayoutElement>();
            iconLayout.preferredWidth = 70f;

            TextMeshProUGUI text = CreateText(row, "GoldText", "0", 52, TextAlignmentOptions.MidlineLeft);
            text.color = new Color(1f, 0.84f, 0f);
            text.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            WalletUI walletUI = row.gameObject.AddComponent<WalletUI>();
            Wire(walletUI, ("_goldText", text), ("_format", format));

            return row;
        }

        private static RectTransform CreateVerticalGroup(Transform parent, string name, float spacing)
        {
            RectTransform rect = CreateRect(name, parent);
            VerticalLayoutGroup layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            return rect;
        }

        // ---------------------------------------------------------------- Project settings

        private static void ConfigureBuildSettings()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MenuScenePath, true),
                new EditorBuildSettingsScene(BattleScenePath, true),
                new EditorBuildSettingsScene(CampScenePath, true)
            };
        }

        private static void ConfigurePlayerSettings()
        {
            // Телефоны — только портрет
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;

            // ПК — вертикальное окно с изменяемым размером (полноэкранный режим тоже работает: поле по центру)
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 540;
            PlayerSettings.defaultScreenHeight = 960;
            PlayerSettings.resizableWindow = true;

            PlayerSettings.defaultWebScreenWidth = 540;
            PlayerSettings.defaultWebScreenHeight = 960;
        }

        // ---------------------------------------------------------------- Serialized field wiring

        private static void Wire(Object target, params (string name, object value)[] values)
        {
            var so = new SerializedObject(target);

            foreach ((string name, object value) in values)
            {
                SerializedProperty property = so.FindProperty(name);
                if (property == null)
                {
                    Debug.LogError($"[Vertical Shooter] {target.GetType().Name}: поле '{name}' не найдено");
                    continue;
                }

                switch (value)
                {
                    case null: property.objectReferenceValue = null; break;
                    case Object obj: property.objectReferenceValue = obj; break;
                    case Enum enumValue: property.intValue = Convert.ToInt32(enumValue); break;
                    case int i: property.intValue = i; break;
                    case float f: property.floatValue = f; break;
                    case bool b: property.boolValue = b; break;
                    case string s: property.stringValue = s; break;
                    case Color c: property.colorValue = c; break;
                    case Vector2 v: property.vector2Value = v; break;
                    case Gradient g: property.gradientValue = g; break;
                    default: Debug.LogError($"[Vertical Shooter] Тип {value.GetType()} не поддерживается ({name})"); break;
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    // При первом открытии проекта предлагает собрать вертикальную версию
    [InitializeOnLoad]
    internal static class VerticalProjectBuilderPrompt
    {
        private const string SessionKey = "VerticalShooter.PromptShown";

        static VerticalProjectBuilderPrompt()
        {
            if (Application.isBatchMode || SessionState.GetBool(SessionKey, false)) return;
            SessionState.SetBool(SessionKey, true);

            EditorApplication.delayCall += () =>
            {
                if (VerticalProjectBuilder.IsBuilt || EditorApplication.isPlayingOrWillChangePlaymode) return;
                VerticalProjectBuilder.BuildAllMenu();
            };
        }
    }
}
