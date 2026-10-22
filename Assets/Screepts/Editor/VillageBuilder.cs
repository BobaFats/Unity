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
    // Деревни: ассеты городов (по 3 уровня на город) и сцена Village.
    // Вызывается из ApplyUpgrades (Update Project (safe)); существующие города и сцена не пересоздаются.
    public static partial class VerticalProjectBuilder
    {
        public const string VillageSceneName = "Village";
        private const string TownsFolder = ResourcesFolder + "/Towns";
        private const string TownCatalogPath = ResourcesFolder + "/" + TownCatalog.ResourcePath + ".asset";
        private const int LevelsPerTown = 3;

        private static string VillageScenePath => $"{SceneFolder}/{VillageSceneName}.unity";

        // Пул городов: в каждом забеге карта собирается из них заново (на карте до 26, проходится 10)
        public const int TownPoolSize = 30;

        private static readonly string[] TownNames =
        {
            "Кастель", "Ромбург", "Сосновый Бор", "Речной Брод", "Каменный Перевал",
            "Туманная Гавань", "Пепельный Холм", "Северная Застава", "Золотой Колос", "Чёрная Цитадель",
            "Вербная Слобода", "Медный Яр", "Старый Мост", "Белая Пристань", "Волчья Падь",
            "Ясногорье", "Дубрава", "Солёный Берег", "Кленовый Двор", "Гнилые Топи",
            "Орлиное Гнездо", "Тихая Заводь", "Ржавый Рудник", "Лунная Роща", "Высокий Престол",
            "Кривая Мельница", "Звонница", "Ледяной Кряж", "Сады Аркадии", "Последний Оплот"
        };

        // ---------------------------------------------------------------- Города

        private static void EnsureTowns()
        {
            EnsureFolder(TownsFolder);

            int townCount = TownPoolSize;
            var towns = new List<TownDefinition>();

            for (int i = 0; i < townCount; i++)
            {
                string path = $"{TownsFolder}/Town_{i + 1:00}.asset";
                var town = AssetDatabase.LoadAssetAtPath<TownDefinition>(path);

                if (town == null)
                {
                    town = ScriptableObject.CreateInstance<TownDefinition>();
                    town.displayName = i < TownNames.Length ? TownNames[i] : $"Город {i + 1}";
                    // Уровни города задаёт его ряд на карте забега; эти значения — запасной вариант без карты
                    town.firstLevel = (i % 10) * LevelsPerTown + 1;
                    town.lastLevel = Mathf.Min(GameStats.TotalLevels, (i % 10 + 1) * LevelsPerTown);
                    town.buildings = CreateDefaultBuildings();
                    AssetDatabase.CreateAsset(town, path);
                }

                towns.Add(town);
            }

            var catalog = AssetDatabase.LoadAssetAtPath<TownCatalog>(TownCatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<TownCatalog>();
                AssetDatabase.CreateAsset(catalog, TownCatalogPath);
            }

            if (catalog.Towns.Count < towns.Count)
            {
                catalog.SetTowns(towns);
                EditorUtility.SetDirty(catalog);
            }

            TownCatalog.SetInstance(catalog);
        }

        // Раскладка по мотивам города Замок из Героев 3: замок в центре, гильдия магов слева,
        // таверна и кузница внизу слева, рынок внизу, ратуша и казармы справа, храм наверху справа, пристань у воды.
        // area — доли панорамы от левого верхнего угла.
        private static List<BuildingDefinition> CreateDefaultBuildings()
        {
            return new List<BuildingDefinition>
            {
                Building("castle", "Замок", new Rect(0.33f, 0.04f, 0.24f, 0.56f), PlaceholderShape.Rectangle, new Color(0.78f, 0.75f, 0.68f),
                    Npc("commandant", "Комендант"), Npc("chronicler", "Летописец")),
                Building("mage_guild", "Гильдия магов", new Rect(0.03f, 0.06f, 0.10f, 0.52f), PlaceholderShape.Triangle, new Color(0.58f, 0.47f, 0.88f),
                    Npc("archmage", "Архимаг")),
                Building("tavern", "Таверна", new Rect(0.02f, 0.62f, 0.17f, 0.34f), PlaceholderShape.Rectangle, new Color(0.68f, 0.47f, 0.3f),
                    Npc("innkeeper", "Трактирщик"), Npc("bard", "Бард")),
                Building("blacksmith", "Кузница", new Rect(0.20f, 0.66f, 0.12f, 0.30f), PlaceholderShape.Rectangle, new Color(0.45f, 0.45f, 0.52f),
                    Npc("smith", "Кузнец")),
                Building("market", "Рынок", new Rect(0.37f, 0.68f, 0.17f, 0.28f), PlaceholderShape.Diamond, new Color(0.92f, 0.76f, 0.35f),
                    Npc("trader", "Торговка"), Npc("changer", "Меняла")),
                Building("town_hall", "Ратуша", new Rect(0.58f, 0.38f, 0.12f, 0.36f), PlaceholderShape.Rectangle, new Color(0.87f, 0.82f, 0.66f),
                    Npc("mayor", "Бургомистр")),
                Building("stables", "Конюшня", new Rect(0.57f, 0.78f, 0.15f, 0.20f), PlaceholderShape.Rectangle, new Color(0.56f, 0.41f, 0.26f),
                    Npc("coachman", "Кучер дилижанса")),
                Building("barracks", "Казармы", new Rect(0.72f, 0.20f, 0.14f, 0.40f), PlaceholderShape.Rectangle, new Color(0.62f, 0.36f, 0.3f),
                    Npc("captain", "Капитан стражи")),
                Building("temple", "Храм", new Rect(0.86f, 0.03f, 0.13f, 0.42f), PlaceholderShape.Triangle, new Color(0.95f, 0.94f, 0.85f),
                    Npc("abbot", "Настоятель")),
                Building("harbor", "Пристань", new Rect(0.84f, 0.62f, 0.15f, 0.34f), PlaceholderShape.Diamond, new Color(0.32f, 0.52f, 0.78f),
                    Npc("boatman", "Лодочник"))
            };
        }

        private static BuildingDefinition Building(string id, string name, Rect area, PlaceholderShape shape, Color color, params NpcDefinition[] npcs)
        {
            return new BuildingDefinition
            {
                id = id,
                displayName = name,
                area = area,
                placeholderShape = shape,
                placeholderColor = color,
                npcs = new List<NpcDefinition>(npcs)
            };
        }

        private static NpcDefinition Npc(string id, string name)
        {
            return new NpcDefinition { id = id, displayName = name };
        }

        // ---------------------------------------------------------------- Сцена деревни

        private static void EnsureVillageScene()
        {
            if (!File.Exists(VillageScenePath)) BuildVillageScene();

            // Деревня в списке сцен сборки
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(s => s.path == VillageScenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(VillageScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
        }

        private static void BuildVillageScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCamera(null);
            CreateEventSystem();

            RectTransform area = CreateCanvas("Canvas");

            TextMeshProUGUI townName = CreateText(area, "TownName", "Город", 96, TextAlignmentOptions.Center);
            SetAnchors(townName.rectTransform, new Vector2(0f, 0.9f), new Vector2(1f, 0.985f));

            // Панорама города: прокрутка пальцем/мышью по горизонтали
            RectTransform view = CreateRect("TownView", area);
            SetAnchors(view, new Vector2(0f, 0.33f), new Vector2(1f, 0.895f));
            AddImage(view, new Color(0f, 0f, 0f, 0.6f));
            view.gameObject.AddComponent<RectMask2D>();

            RectTransform content = CreateRect("Content", view);
            content.anchorMin = new Vector2(0f, 0f);
            content.anchorMax = new Vector2(0f, 1f);
            content.pivot = new Vector2(0f, 0.5f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = new Vector2(2300f, 0f);

            RectTransform sky = CreateRect("Sky", content);
            Stretch(sky);
            Image skyImage = sky.gameObject.AddComponent<Image>();
            skyImage.raycastTarget = false;

            RectTransform ground = CreateRect("Ground", content);
            SetAnchors(ground, new Vector2(0f, 0f), new Vector2(1f, 0.42f));
            Image groundImage = ground.gameObject.AddComponent<Image>();
            groundImage.raycastTarget = false;

            ScrollRect scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = view;
            scroll.content = content;
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = true;

            // Шаблон здания: картинка + подпись внизу
            Button buildingTemplate = CreateButton(content, "BuildingTemplate", "Здание", Color.white, 34);
            TextMeshProUGUI buildingLabel = buildingTemplate.GetComponentInChildren<TextMeshProUGUI>();
            SetAnchors(buildingLabel.rectTransform, new Vector2(-0.2f, 0f), new Vector2(1.2f, 0.22f));
            buildingLabel.color = new Color(0.08f, 0.08f, 0.08f);
            buildingLabel.fontStyle = FontStyles.Bold;

            TextMeshProUGUI info = CreateText(area, "Info", "Нажмите на здание", 40, TextAlignmentOptions.Center);
            SetAnchors(info.rectTransform, new Vector2(0.04f, 0.25f), new Vector2(0.96f, 0.32f));

            RectTransform buttons = CreateVerticalGroup(area, "Buttons", 36);
            SetAnchors(buttons, new Vector2(0.12f, 0.03f), new Vector2(0.88f, 0.23f));
            Button next = CreateButton(buttons, "ContinueButton", "Продолжить путь", AccentButtonColor, 72);
            Wire(next.gameObject.AddComponent<SceneLoadButton>(), ("_sceneName", LobbySceneName), ("_resetProgress", false));
            Button menu = CreateButton(buttons, "MenuButton", "Главное меню", ButtonColor, 56);
            Wire(menu.gameObject.AddComponent<SceneLoadButton>(), ("_sceneName", MenuSceneName), ("_resetProgress", false));

            // Окно здания со списком жителей
            RectTransform panel = CreateRect("BuildingPanel", area);
            Stretch(panel);
            AddImage(panel, DimColor);

            RectTransform window = CreateRect("Window", panel);
            SetAnchors(window, new Vector2(0.05f, 0.18f), new Vector2(0.95f, 0.82f));
            AddImage(window, PanelColor);

            VerticalLayoutGroup layout = window.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(40, 40, 40, 40);
            layout.spacing = 24f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            TextMeshProUGUI buildingTitle = CreateText(window, "Title", "Здание", 80, TextAlignmentOptions.Center);
            AddLayoutHeight(buildingTitle.gameObject, 110);
            TextMeshProUGUI buildingDescription = CreateText(window, "Description", "", 40, TextAlignmentOptions.Center);
            AddLayoutHeight(buildingDescription.gameObject, 110);

            RectTransform npcList = CreateRect("Npcs", window);
            VerticalLayoutGroup npcLayout = npcList.gameObject.AddComponent<VerticalLayoutGroup>();
            npcLayout.spacing = 20f;
            npcLayout.childControlWidth = true;
            npcLayout.childControlHeight = true;
            npcLayout.childForceExpandWidth = true;
            npcLayout.childForceExpandHeight = false;
            npcList.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;

            Button npcTemplate = CreateButton(npcList, "NpcTemplate", "Житель", new Color(0.22f, 0.24f, 0.32f), 52);
            AddLayoutHeight(npcTemplate.gameObject, 170);
            TextMeshProUGUI npcName = npcTemplate.GetComponentInChildren<TextMeshProUGUI>();
            npcName.alignment = TextAlignmentOptions.MidlineLeft;
            npcName.rectTransform.offsetMin = new Vector2(200f, 10f);

            RectTransform portrait = CreateRect("Portrait", npcTemplate.transform);
            SetAnchors(portrait, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
            portrait.anchoredPosition = new Vector2(30f, 0f);
            portrait.sizeDelta = new Vector2(140f, 140f);
            portrait.gameObject.AddComponent<Image>().raycastTarget = false;

            Button close = CreateButton(window, "CloseButton", "Выйти из здания", DangerButtonColor, 52);
            AddLayoutHeight(close.gameObject, 120);
            panel.gameObject.SetActive(false);

            DialoguePanel dialogue = BuildDialogue(area);

            VillageController controller = new GameObject("Village").AddComponent<VillageController>();
            Wire(controller,
                ("_townName", townName),
                ("_infoText", info),
                ("_townView", scroll),
                ("_content", content),
                ("_sky", skyImage),
                ("_ground", groundImage),
                ("_buildingTemplate", buildingTemplate),
                ("_buildingPanel", panel.gameObject),
                ("_buildingTitle", buildingTitle),
                ("_buildingDescription", buildingDescription),
                ("_npcContainer", npcList),
                ("_npcTemplate", npcTemplate),
                ("_closeBuildingButton", close),
                ("_dialogue", dialogue));

            EditorSceneManager.SaveScene(scene, VillageScenePath);
        }
    }
}
