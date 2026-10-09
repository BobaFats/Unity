using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Tanks2D.EditorTools
{
    // Безопасное обновление проекта: добавляет недостающее (ассеты, компоненты, UI) в СУЩЕСТВУЮЩИЕ
    // сцены и префабы, ничего не пересоздавая. Ручные правки (модели, анимации, расстановка) сохраняются.
    // Каждый шаг можно запускать повторно — уже сделанное он пропускает.
    // Меню: Tools > Vertical Shooter > Update Project (safe).
    public static partial class VerticalProjectBuilder
    {
        private const string LevelsFolder = ResourcesFolder + "/Levels";
        private const string LevelCatalogPath = ResourcesFolder + "/" + LevelCatalog.ResourcePath + ".asset";
        private const string SkinsFolder = ResourcesFolder + "/WeaponSkins";
        private const string SkinCatalogPath = ResourcesFolder + "/" + WeaponSkinCatalog.ResourcePath + ".asset";

        [MenuItem(MenuRoot + "Update Project (safe)", priority = 1)]
        public static void UpdateProjectMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            string current = SceneManager.GetActiveScene().path;
            ApplyUpgrades();
            if (!string.IsNullOrEmpty(current)) EditorSceneManager.OpenScene(current);

            EditorUtility.DisplayDialog("Vertical Shooter", "Проект обновлён. Ручные правки в сценах и префабах сохранены.", "OK");
        }

        // Для запуска из командной строки: -executeMethod Tanks2D.EditorTools.VerticalProjectBuilder.UpdateProjectBatch
        public static void UpdateProjectBatch()
        {
            ApplyUpgrades();
        }

        public static void ApplyUpgrades()
        {
            EnsureFolder(ResourcesFolder);
            CreateOrUpdateCatalog();
            EnsureLevels();
            EnsureWeaponSkins();
            UpgradeBossPrefab();
            RebalanceEnemyPrefabs();
            EnsureYogSothoth();
            UpgradeBattleScene();
            UpgradeLobbyScene();
            AssetDatabase.SaveAssets();
            Debug.Log("[Vertical Shooter] Обновление проекта завершено");
        }

        // ---------------------------------------------------------------- 30 уровней

        private static void EnsureLevels()
        {
            EnsureFolder(LevelsFolder);

            var levels = new List<LevelDefinition>();
            for (int i = 1; i <= GameStats.TotalLevels; i++)
            {
                string path = $"{LevelsFolder}/Level_{i:00}.asset";
                var level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(path);

                if (level == null)
                {
                    level = ScriptableObject.CreateInstance<LevelDefinition>();
                    level.levelNumber = i;
                    level.title = "";
                    level.killsForBoss = KillsForBoss(i);
                    // 1-й босс — только щит; со 2-го — щит, таран стены и разрушение башен
                    level.bossAbilities = i == 1
                        ? BossAbility.Shield
                        : BossAbility.Shield | BossAbility.RamWall | BossAbility.DestroyTowers;
                    AssetDatabase.CreateAsset(level, path);
                }

                levels.Add(level);
            }

            var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(LevelCatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<LevelCatalog>();
                AssetDatabase.CreateAsset(catalog, LevelCatalogPath);
            }

            if (catalog.Count < levels.Count)
            {
                catalog.SetLevels(levels);
                EditorUtility.SetDirty(catalog);
            }

            LevelCatalog.SetInstance(catalog);
        }

        // ---------------------------------------------------------------- Скины оружия

        private static void EnsureWeaponSkins()
        {
            EnsureFolder(SkinsFolder);

            var catalog = AssetDatabase.LoadAssetAtPath<WeaponSkinCatalog>(SkinCatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<WeaponSkinCatalog>();
                AssetDatabase.CreateAsset(catalog, SkinCatalogPath);
            }

            catalog.Add(CreateSkin("Standard", "Стандарт", null, Color.white));
            catalog.Add(CreateSkin("Blaster", "Бластер", AssetDatabase.LoadAssetAtPath<Sprite>($"{PlaceholderFolder}/{PlaceholderShape.Diamond}.png"), new Color(0.4f, 0.9f, 1f)));
            catalog.Add(CreateSkin("Golden", "Золотое", null, new Color(1f, 0.82f, 0.2f)));

            EditorUtility.SetDirty(catalog);
            WeaponSkinCatalog.SetInstance(catalog);
        }

        private static WeaponSkin CreateSkin(string fileName, string displayName, Sprite sprite, Color tint)
        {
            string path = $"{SkinsFolder}/{fileName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<WeaponSkin>(path);
            if (existing != null) return existing;

            var skin = ScriptableObject.CreateInstance<WeaponSkin>();
            skin.displayName = displayName;
            skin.sprite = sprite;
            skin.tint = tint;
            AssetDatabase.CreateAsset(skin, path);
            return skin;
        }

        // ---------------------------------------------------------------- Босс: полоска щита в префабе

        private static void UpgradeBossPrefab()
        {
            string path = $"{PrefabFolder}/Boss_Pig.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) return;

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            bool changed = false;

            BossAbilities abilities = root.GetComponent<BossAbilities>();
            if (abilities != null && new SerializedObject(abilities).FindProperty("_shieldBar").objectReferenceValue == null)
            {
                EnemyHealthBar2D healthBar = root.GetComponentInChildren<EnemyHealthBar2D>(true);
                var go = new GameObject("ShieldBar");
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = healthBar != null ? healthBar.transform.localPosition + new Vector3(0f, 0.2f, 0f) : new Vector3(0f, 1.6f, 0f);

                EnemyHealthBar2D shieldBar = go.AddComponent<EnemyHealthBar2D>();
                shieldBar.Build(PlaceholderSprites.Get(PlaceholderShape.Rectangle), healthBar != null ? healthBar.Size : new Vector2(2.4f, 0.14f), 44);
                shieldBar.SetFillColor(new Color(0.35f, 0.65f, 1f));
                go.SetActive(false);

                Wire(abilities, ("_shieldBar", shieldBar));
                changed = true;
            }

            if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        // ---------------------------------------------------------------- Бой: новая кривая опыта

        private static void UpgradeBattleScene()
        {
            if (!System.IO.File.Exists(BattleScenePath)) return;

            Scene scene = EditorSceneManager.OpenScene(BattleScenePath);
            bool changed = false;

            // Кривую опыта меняем, только если она старая (5 / 1.35) — ручные настройки не трогаем
            ExperienceSystem experience = Object.FindAnyObjectByType<ExperienceSystem>();
            if (experience != null)
            {
                var so = new SerializedObject(experience);
                SerializedProperty baseXp = so.FindProperty("_baseXpToLevel");
                SerializedProperty growth = so.FindProperty("_xpGrowth");
                if (baseXp.intValue == 5 && Mathf.Approximately(growth.floatValue, 1.35f))
                {
                    baseXp.intValue = ExperienceSystem.DefaultBaseXp;
                    growth.floatValue = ExperienceSystem.DefaultXpGrowth;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    changed = true;
                }
            }

            changed |= RebalanceSpawner();

            if (changed) EditorSceneManager.SaveScene(scene);
        }

        // ---------------------------------------------------------------- Баланс «мясной шутер»
        // Значения меняются, только если они ещё стоят по умолчанию — ручные настройки не трогаем.

        private static int KillsForBoss(int level) => 150 + 25 * (level - 1);

        private static void RebalanceEnemyPrefabs()
        {
            SetPrefabFieldIf($"{PrefabFolder}/Enemy_Pig.prefab", "maxHP", 30, 8);
            SetPrefabFieldIf($"{PrefabFolder}/Enemy_Chicken.prefab", "maxHP", 15, 5);

            // Больше врагов до босса: 150 на 1-м уровне и +25 за каждый следующий
            LevelCatalog catalog = LevelCatalog.Instance;
            if (catalog == null) return;
            foreach (LevelDefinition level in catalog.Levels)
            {
                if (level == null || level.killsForBoss != 60) continue;
                level.killsForBoss = KillsForBoss(level.levelNumber);
                EditorUtility.SetDirty(level);
            }
        }

        private static void SetPrefabFieldIf(string path, string field, int oldValue, int newValue)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) return;

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            PigEnemy enemy = root.GetComponent<PigEnemy>();
            bool changed = false;

            if (enemy != null)
            {
                var so = new SerializedObject(enemy);
                SerializedProperty property = so.FindProperty(field);
                if (property != null && property.intValue == oldValue)
                {
                    property.intValue = newValue;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    changed = true;
                }
            }

            if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        private static bool RebalanceSpawner()
        {
            EnemySpawner2D spawner = Object.FindAnyObjectByType<EnemySpawner2D>();
            if (spawner == null) return false;

            var so = new SerializedObject(spawner);
            bool changed = false;
            changed |= SetIf(so.FindProperty("_spawnRate"), 2f, 1.5f);
            changed |= SetIf(so.FindProperty("_maxAliveEnemies"), 50, 80);
            changed |= SetIf(so.FindProperty("killsNeededForBoss"), 60, 150);
            changed |= SetIf(so.FindProperty("_healthGrowthPerPlayerLevel"), 0.15f, 0.08f);

            // Босс больше не убирает остальных врагов: они продолжают бой, новые не появляются
            SerializedProperty clearOnBoss = so.FindProperty("_clearEnemiesOnBoss");
            if (clearOnBoss != null && clearOnBoss.boolValue)
            {
                clearOnBoss.boolValue = false;
                changed = true;
            }

            SerializedProperty duringBoss = so.FindProperty("_spawnDuringBoss");
            if (duringBoss != null && duringBoss.boolValue)
            {
                duringBoss.boolValue = false;
                changed = true;
            }

            if (changed) so.ApplyModifiedPropertiesWithoutUndo();
            return changed;
        }

        private static bool SetIf(SerializedProperty property, float oldValue, float newValue)
        {
            if (property == null) return false;

            if (property.propertyType == SerializedPropertyType.Integer)
            {
                if (property.intValue != Mathf.RoundToInt(oldValue)) return false;
                property.intValue = Mathf.RoundToInt(newValue);
                return true;
            }

            if (!Mathf.Approximately(property.floatValue, oldValue)) return false;
            property.floatValue = newValue;
            return true;
        }

        // ---------------------------------------------------------------- Йог-Сотот (мини-босс уровней 4+)

        private const int YogFirstLevel = 4;

        private static void EnsureYogSothoth()
        {
            string tentaclePath = $"{PrefabFolder}/Tentacle.prefab";
            string yogPath = $"{PrefabFolder}/Yog_Sothoth.prefab";

            var prefabs = new Prefabs { DamageText = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/FloatingText_Damage.prefab") };

            // Щупальце: стоит на месте, уязвимо
            GameObject tentacle = AssetDatabase.LoadAssetAtPath<GameObject>(tentaclePath);
            if (tentacle == null)
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                tentacle = CreateEnemyPrefab("Tentacle", VisualId.Tentacle, 25, 0f, 0, 3, 1f, false, prefabs);
            }

            // Йог-Сотот: быстрый, неуязвим до гибели щупалец
            GameObject yog = AssetDatabase.LoadAssetAtPath<GameObject>(yogPath);
            if (yog == null)
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                CreateEnemyPrefab("Yog_Sothoth", VisualId.YogSothoth, 150, 4.5f, 15, 25, 0.8f, false, prefabs);

                GameObject root = PrefabUtility.LoadPrefabContents(yogPath);
                YogSothoth component = root.AddComponent<YogSothoth>();
                Wire(component, ("_tentaclePrefab", tentacle));
                PrefabUtility.SaveAsPrefabAsset(root, yogPath);
                PrefabUtility.UnloadPrefabContents(root);
                yog = AssetDatabase.LoadAssetAtPath<GameObject>(yogPath);
            }

            // Добавляем Йога мини-боссом на уровни 4..30 (если его там ещё нет)
            LevelCatalog catalog = LevelCatalog.Instance;
            if (catalog == null || yog == null) return;

            foreach (LevelDefinition level in catalog.Levels)
            {
                if (level == null || level.levelNumber < YogFirstLevel) continue;
                if (level.miniBosses.Exists(m => m != null && m.prefab == yog)) continue;

                level.miniBosses.Add(new MiniBossSpawn
                {
                    prefab = yog,
                    afterKills = Mathf.Max(1, level.killsForBoss / 3),
                    abilities = BossAbility.None,
                    healthMultiplier = 1f
                });
                EditorUtility.SetDirty(level);
            }
        }

        // ---------------------------------------------------------------- Лобби: выбор скина оружия

        private static void UpgradeLobbyScene()
        {
            if (!System.IO.File.Exists(LobbyScenePath)) return;

            Scene scene = EditorSceneManager.OpenScene(LobbyScenePath);
            if (Object.FindAnyObjectByType<WeaponSkinSelector>() != null) return;

            LobbyUI lobby = Object.FindAnyObjectByType<LobbyUI>();
            if (lobby == null) return;
            var area = (RectTransform)lobby.transform;

            // Освобождаем место под строку выбора между характеристиками и кнопками
            Transform stats = area.Find("StatsPanel");
            if (stats != null)
            {
                var statsRect = (RectTransform)stats;
                statsRect.anchorMin = new Vector2(statsRect.anchorMin.x, 0.42f);
            }

            BuildSkinSelector(area);
            EditorSceneManager.SaveScene(scene);
        }

        private static void BuildSkinSelector(RectTransform area)
        {
            RectTransform row = CreateRect("WeaponSkinSelector", area);
            SetAnchors(row, new Vector2(0.05f, 0.31f), new Vector2(0.95f, 0.40f));
            AddImage(row, PanelColor);

            Button previous = CreateButton(row, "PreviousSkin", "<", ButtonColor, 64);
            SetAnchors((RectTransform)previous.transform, new Vector2(0f, 0.1f), new Vector2(0f, 0.9f), new Vector2(0f, 0.5f));
            ((RectTransform)previous.transform).anchoredPosition = new Vector2(20f, 0f);
            ((RectTransform)previous.transform).sizeDelta = new Vector2(130f, 0f);

            Button next = CreateButton(row, "NextSkin", ">", ButtonColor, 64);
            SetAnchors((RectTransform)next.transform, new Vector2(1f, 0.1f), new Vector2(1f, 0.9f), new Vector2(1f, 0.5f));
            ((RectTransform)next.transform).anchoredPosition = new Vector2(-20f, 0f);
            ((RectTransform)next.transform).sizeDelta = new Vector2(130f, 0f);

            RectTransform iconRect = CreateRect("Icon", row);
            SetAnchors(iconRect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
            iconRect.anchoredPosition = new Vector2(180f, 0f);
            iconRect.sizeDelta = new Vector2(110f, 110f);
            Image icon = iconRect.gameObject.AddComponent<Image>();
            icon.raycastTarget = false;

            TextMeshProUGUI name = CreateText(row, "SkinName", "Оружие: Стандарт", 46, TextAlignmentOptions.MidlineLeft);
            Stretch(name.rectTransform);
            name.rectTransform.offsetMin = new Vector2(310f, 10f);
            name.rectTransform.offsetMax = new Vector2(-170f, -10f);

            WeaponSkinSelector selector = row.gameObject.AddComponent<WeaponSkinSelector>();
            Wire(selector, ("_previous", previous), ("_next", next), ("_nameText", name), ("_icon", icon));
        }
    }
}
