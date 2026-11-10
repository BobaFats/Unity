using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Tanks2D.EditorTools
{
    // Конец забега и наследие: каталог улучшений, сцена итогов RunEnd, окно «Наследие» в меню,
    // кнопка «Завершить поход» в лобби, «Повторить уровень» на экране поражения.
    // Вызывается из ApplyUpgrades (Update Project (safe)); существующее не пересоздаётся.
    public static partial class VerticalProjectBuilder
    {
        private const string MetaCatalogPath = ResourcesFolder + "/" + MetaUpgradeCatalog.ResourcePath + ".asset";
        private static string RunEndScenePath => $"{SceneFolder}/{RunSummary.RunEndSceneName}.unity";

        private static void EnsureMeta()
        {
            EnsureMetaCatalog();
            if (!File.Exists(RunEndScenePath)) BuildRunEndScene();

            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(s => s.path == RunEndScenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(RunEndScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }

            UpgradeMenuForLegacy();
            UpgradeLobbyForRunFlow();
        }

        // ---------------------------------------------------------------- Каталог наследия

        private static void EnsureMetaCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<MetaUpgradeCatalog>(MetaCatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<MetaUpgradeCatalog>();
                AssetDatabase.CreateAsset(catalog, MetaCatalogPath);
            }

            bool changed = false;
            changed |= catalog.AddIfMissing(new MetaUpgrade { id = "wall", title = "Крепкая стена", description = "+{0}% прочности стены", effect = MetaEffect.WallHPPercent, amountPerLevel = 10, maxLevel = 5, baseCost = 40, costGrowth = 1.6f });
            changed |= catalog.AddIfMissing(new MetaUpgrade { id = "damage", title = "Острые пули", description = "+{0}% урона", effect = MetaEffect.DamagePercent, amountPerLevel = 5, maxLevel = 5, baseCost = 50, costGrowth = 1.7f });
            changed |= catalog.AddIfMissing(new MetaUpgrade { id = "horseshoes", title = "Запас подков", description = "+{0} подков на старте", effect = MetaEffect.StartHorseshoes, amountPerLevel = 3, maxLevel = 3, baseCost = 30, costGrowth = 1.8f });
            changed |= catalog.AddIfMissing(new MetaUpgrade { id = "tower", title = "Своя башня", description = "+{0} башня на старте", effect = MetaEffect.StartTowers, amountPerLevel = 1, maxLevel = 2, baseCost = 120, costGrowth = 2.5f });

            if (changed) EditorUtility.SetDirty(catalog);
            MetaUpgradeCatalog.SetInstance(catalog);
        }

        // ---------------------------------------------------------------- Сцена итогов

        private static void BuildRunEndScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCamera(null);
            CreateEventSystem();

            RectTransform area = CreateCanvas("Canvas");

            TextMeshProUGUI title = CreateText(area, "Title", "ПОХОД ОКОНЧЕН", 110, TextAlignmentOptions.Center);
            SetAnchors(title.rectTransform, new Vector2(0f, 0.84f), new Vector2(1f, 0.96f));

            RectTransform statsPanel = CreateRect("StatsPanel", area);
            SetAnchors(statsPanel, new Vector2(0.05f, 0.4f), new Vector2(0.95f, 0.82f));
            AddImage(statsPanel, PanelColor);

            TextMeshProUGUI stats = CreateText(statsPanel, "StatsText", "", 52, TextAlignmentOptions.TopLeft);
            Stretch(stats.rectTransform);
            stats.rectTransform.offsetMin = new Vector2(40f, 40f);
            stats.rectTransform.offsetMax = new Vector2(-40f, -40f);

            TextMeshProUGUI glory = CreateText(area, "GloryText", "", 56, TextAlignmentOptions.Center);
            SetAnchors(glory.rectTransform, new Vector2(0.05f, 0.29f), new Vector2(0.95f, 0.38f));
            glory.color = new Color(1f, 0.85f, 0.35f);

            RectTransform buttons = CreateVerticalGroup(area, "Buttons", 36);
            SetAnchors(buttons, new Vector2(0.12f, 0.05f), new Vector2(0.88f, 0.26f));
            Button legacy = CreateButton(buttons, "LegacyButton", "Наследие", AccentButtonColor, 72);
            Button menu = CreateButton(buttons, "MenuButton", "Главное меню", ButtonColor, 64);

            BuildLegacyPanel(area, legacy);

            RunEndScreen screen = new GameObject("RunEnd").AddComponent<RunEndScreen>();
            Wire(screen,
                ("_title", title),
                ("_stats", stats),
                ("_glory", glory),
                ("_menuButton", menu),
                ("_menuScene", MenuSceneName));

            EditorSceneManager.SaveScene(scene, RunEndScenePath);
        }

        // Окно «Наследие»: слава и список улучшений
        private static LegacyPanel BuildLegacyPanel(RectTransform area, Button openButton)
        {
            RectTransform holder = CreateRect("Legacy", area);
            Stretch(holder);
            LegacyPanel legacy = holder.gameObject.AddComponent<LegacyPanel>();

            RectTransform root = CreateRect("Panel", holder);
            Stretch(root);
            AddImage(root, DimColor);

            RectTransform window = CreateRect("Window", root);
            SetAnchors(window, new Vector2(0.04f, 0.06f), new Vector2(0.96f, 0.94f));
            AddImage(window, PanelColor);

            VerticalLayoutGroup layout = window.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(36, 36, 36, 36);
            layout.spacing = 22f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            AddLayoutHeight(CreateText(window, "Title", "НАСЛЕДИЕ", 84, TextAlignmentOptions.Center).gameObject, 110);
            TextMeshProUGUI hint = CreateText(window, "Hint", "Улучшения действуют на каждый новый поход. Слава даётся за пройденные уровни, задания и победу.", 34, TextAlignmentOptions.Center);
            AddLayoutHeight(hint.gameObject, 100);
            TextMeshProUGUI glory = CreateText(window, "Glory", "Слава: 0", 56, TextAlignmentOptions.Center);
            glory.color = new Color(1f, 0.85f, 0.35f);
            AddLayoutHeight(glory.gameObject, 80);

            RectTransform list = CreateRect("Upgrades", window);
            VerticalLayoutGroup listLayout = list.gameObject.AddComponent<VerticalLayoutGroup>();
            listLayout.spacing = 18f;
            listLayout.childControlWidth = true;
            listLayout.childControlHeight = true;
            listLayout.childForceExpandWidth = true;
            listLayout.childForceExpandHeight = false;
            list.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;

            Button row = CreateButton(list, "UpgradeTemplate", "Улучшение", new Color(0.22f, 0.24f, 0.32f), 46);
            AddLayoutHeight(row.gameObject, 170);
            TextMeshProUGUI rowText = row.GetComponentInChildren<TextMeshProUGUI>();
            rowText.alignment = TextAlignmentOptions.MidlineLeft;
            rowText.rectTransform.offsetMin = new Vector2(30f, 8f);
            rowText.rectTransform.offsetMax = new Vector2(-30f, -8f);

            Button close = CreateButton(window, "CloseButton", "Закрыть", DangerButtonColor, 56);
            AddLayoutHeight(close.gameObject, 130);

            Wire(legacy,
                ("_root", root.gameObject),
                ("_gloryText", glory),
                ("_container", list),
                ("_rowTemplate", row),
                ("_closeButton", close),
                ("_openButton", openButton));

            root.gameObject.SetActive(false);
            return legacy;
        }

        // ---------------------------------------------------------------- Меню: «Наследие»

        private static void UpgradeMenuForLegacy()
        {
            if (!File.Exists(MenuScenePath)) return;

            Scene scene = EditorSceneManager.OpenScene(MenuScenePath);
            if (Object.FindAnyObjectByType<LegacyPanel>(FindObjectsInactive.Include) != null) return;

            MainMenuController menu = Object.FindAnyObjectByType<MainMenuController>();
            Button settings = menu != null ? new SerializedObject(menu).FindProperty("settingsButton").objectReferenceValue as Button : null;
            if (settings == null) return;

            Transform buttons = settings.transform.parent;
            var area = (RectTransform)buttons.parent;

            Button legacy = CreateButton(buttons, "LegacyButton", "Наследие", ButtonColor, 160);
            legacy.transform.SetSiblingIndex(settings.transform.GetSiblingIndex());
            BuildLegacyPanel(area, legacy);

            EditorSceneManager.SaveScene(scene);
        }

        // ---------------------------------------------------------------- Лобби: «Завершить поход»

        private static void UpgradeLobbyForRunFlow()
        {
            if (!File.Exists(LobbyScenePath)) return;

            Scene scene = EditorSceneManager.OpenScene(LobbyScenePath);
            if (Object.FindAnyObjectByType<RunFlowButton>(FindObjectsInactive.Include) != null) return;

            GameObject start = GameObject.Find("StartMissionButton");
            if (start == null) return;

            Button finish = CreateButton(start.transform.parent, "FinishRunButton", "Завершить поход", DangerButtonColor, 56);
            Wire(finish.gameObject.AddComponent<RunFlowButton>(), ("_action", RunFlowButton.Action.FinishRun));

            EditorSceneManager.SaveScene(scene);
        }

        // ---------------------------------------------------------------- Бой: экран поражения
        // Только кнопки экрана поражения — остальную сцену (модель героя и пр.) не трогаем

        private static bool UpgradeGameOverButtons()
        {
            Wall wall = Object.FindAnyObjectByType<Wall>();
            var panel = wall != null ? new SerializedObject(wall).FindProperty("gameOverPanel").objectReferenceValue as GameObject : null;
            if (panel == null || panel.GetComponentInChildren<RunFlowButton>(true) != null) return false;

            bool changed = false;
            foreach (SceneLoadButton button in panel.GetComponentsInChildren<SceneLoadButton>(true))
            {
                bool reset = new SerializedObject(button).FindProperty("_resetProgress").boolValue;
                GameObject go = button.gameObject;
                Object.DestroyImmediate(button);

                Wire(go.AddComponent<RunFlowButton>(), ("_action", reset ? RunFlowButton.Action.FinishRun : RunFlowButton.Action.RetryLevel), ("_battleScene", BattleSceneName));
                TextMeshProUGUI label = go.GetComponentInChildren<TextMeshProUGUI>(true);
                if (label != null) label.text = reset ? "Завершить поход" : "Повторить уровень";
                changed = true;
            }
            return changed;
        }
    }
}
