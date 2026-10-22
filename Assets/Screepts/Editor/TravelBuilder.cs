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
    // Карта забега (Map), поездка на дилижансе (Stagecoach) и колода встреч.
    // Вызывается из ApplyUpgrades (Update Project (safe)); существующие сцены и ассеты не пересоздаются.
    public static partial class VerticalProjectBuilder
    {
        public const string MapSceneName = "Map";
        public const string StagecoachSceneName = "Stagecoach";
        private const string EncountersFolder = ResourcesFolder + "/Encounters";
        private const string EncounterCatalogPath = ResourcesFolder + "/" + EncounterCatalog.ResourcePath + ".asset";

        private static string MapScenePath => $"{SceneFolder}/{MapSceneName}.unity";
        private static string StagecoachScenePath => $"{SceneFolder}/{StagecoachSceneName}.unity";

        private static void EnsureTravel()
        {
            EnsureEncounters();
            if (!File.Exists(MapScenePath)) BuildMapScene();
            if (!File.Exists(StagecoachScenePath)) BuildStagecoachScene();

            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (string path in new[] { MapScenePath, StagecoachScenePath })
            {
                if (!scenes.Exists(s => s.path == path)) scenes.Add(new EditorBuildSettingsScene(path, true));
            }
            EditorBuildSettings.scenes = scenes.ToArray();

            UpgradeVillageContinue();
        }

        // Кнопка «Продолжить» в деревне: решает VillageController (карта похода или лобби)
        private static void UpgradeVillageContinue()
        {
            if (!File.Exists(VillageScenePath)) return;

            Scene scene = EditorSceneManager.OpenScene(VillageScenePath);
            VillageController village = Object.FindAnyObjectByType<VillageController>();
            GameObject button = GameObject.Find("ContinueButton");
            if (village == null || button == null) return;

            var so = new SerializedObject(village);
            if (so.FindProperty("_continueButton").objectReferenceValue != null) return;

            SceneLoadButton old = button.GetComponent<SceneLoadButton>();
            if (old != null) Object.DestroyImmediate(old);

            Wire(village, ("_continueButton", button.GetComponent<Button>()));
            EditorSceneManager.SaveScene(scene);
        }

        // ---------------------------------------------------------------- Встречи (Hand of Fate)

        private static void EnsureEncounters()
        {
            EnsureFolder(EncountersFolder);

            var catalog = AssetDatabase.LoadAssetAtPath<EncounterCatalog>(EncounterCatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<EncounterCatalog>();
                AssetDatabase.CreateAsset(catalog, EncounterCatalogPath);
            }

            catalog.Add(Encounter("WoundedTraveler", "Раненый путник", "У обочины сидит путник с перевязанной ногой и просит подвезти до города.",
                new Color(0.6f, 0.5f, 0.35f),
                Gamble("Подвезти",
                    Chance(ChanceResult.HugeSuccess, 1, "Путник оказался ветераном и поделился опытом.", Fx(EncounterEffectType.Experience, 25), Fx(EncounterEffectType.Horseshoes, 3)),
                    Chance(ChanceResult.Success, 2, "Благодарный путник делится припасами.", Fx(EncounterEffectType.Experience, 12)),
                    Chance(ChanceResult.Failure, 2, "Лишний вес — колесо треснуло.", Fx(EncounterEffectType.Wheels, -1)),
                    Chance(ChanceResult.HugeFailure, 1, "Это был разбойник! Обчистил и сбежал.", Fx(EncounterEffectType.Horseshoes, -5), Fx(EncounterEffectType.Wheels, -1))),
                Direct("Проехать мимо", "Путник остался позади.")));

            catalog.Add(Encounter("AmuletTrader", "Торговец амулетами", "Пёстрый торговец машет амулетом: «Пули сами найдут цель!»",
                new Color(0.75f, 0.55f, 0.85f),
                Direct("Купить амулет", "Амулет тёплый на ощупь.", 5, Fx(EncounterEffectType.BulletDamagePercent, 10)),
                Direct("Отказаться", "Торговец пожимает плечами.")));

            catalog.Add(Encounter("Bandits", "Разбойники на дороге", "Дорогу перегородили разбойники с дубинами.",
                new Color(0.55f, 0.3f, 0.25f),
                Gamble("Прорваться",
                    Chance(ChanceResult.HugeSuccess, 1, "Разбойники разбежались, бросив добычу.", Fx(EncounterEffectType.Horseshoes, 6)),
                    Chance(ChanceResult.Success, 2, "Прорвались без потерь."),
                    Chance(ChanceResult.Failure, 2, "Колесо разбито о завал.", Fx(EncounterEffectType.Wheels, -1)),
                    Chance(ChanceResult.HugeFailure, 1, "Погоня до самого города — разбойники добрались и до стены.", Fx(EncounterEffectType.Wheels, -1), Fx(EncounterEffectType.WallDamageNextBattle, 15))),
                Direct("Откупиться", "Разбойники пропустили дилижанс.", 4),
                Direct("Объехать лесом", "По корням и кочкам — колесо не выдержало.", 0, Fx(EncounterEffectType.Wheels, -1))));

            catalog.Add(Encounter("AbandonedForge", "Заброшенная кузница", "У дороги пустая кузница, угли в горне ещё тёплые.",
                new Color(0.45f, 0.42f, 0.48f),
                Direct("Починить колесо", "Колесо как новое.", 0, Fx(EncounterEffectType.Wheels, 1)),
                Gamble("Поискать оружие",
                    Chance(ChanceResult.HugeSuccess, 1, "Отличный клинок для пуль — кузнец знал толк.", Fx(EncounterEffectType.BulletDamagePercent, 15)),
                    Chance(ChanceResult.Success, 2, "Нашлись хорошие пули.", Fx(EncounterEffectType.BulletDamagePercent, 8)),
                    Chance(ChanceResult.Failure, 2, "Ничего полезного."),
                    Chance(ChanceResult.HugeFailure, 1, "Обжглись о горн и растеряли подковы.", Fx(EncounterEffectType.Horseshoes, -2)))));

            catalog.Add(Encounter("RuneAltar", "Странный алтарь", "Камень с рунами переливается цветами всех стихий.",
                new Color(0.35f, 0.6f, 0.75f),
                Gamble("Прикоснуться к рунам",
                    Chance(ChanceResult.HugeSuccess, 1, "Руны вспыхнули — сила стихий с вами.", Fx(EncounterEffectType.ElementLevel, 2)),
                    Chance(ChanceResult.Success, 2, "Одна из рун откликнулась.", Fx(EncounterEffectType.ElementLevel, 1)),
                    Chance(ChanceResult.Failure, 2, "Руны погасли."),
                    Chance(ChanceResult.HugeFailure, 1, "Удар молнии! Отзовётся и на стене.", Fx(EncounterEffectType.WallDamageNextBattle, 20))),
                Direct("Не трогать", "Лучше не связываться.")));

            catalog.Add(Encounter("Carpenters", "Плотники у моста", "Плотники чинят мост и просят помочь с брёвнами.",
                new Color(0.65f, 0.5f, 0.3f),
                Direct("Помочь", "В благодарность плотники сколотят вам башню-помощника.", 0, Fx(EncounterEffectType.AddTower, 1)),
                Direct("Нет времени", "Мост скрипит, но держит.")));

            catalog.Add(Encounter("FortuneTeller", "Гадалка у реки", "Гадалка раскладывает карты: «Позолоти ручку — скажу судьбу».",
                new Color(0.85f, 0.45f, 0.4f),
                Gamble("Погадать",
                    Chance(ChanceResult.HugeSuccess, 1, "«Тебя ждёт великая слава!» — и правда, на душе легче.", Fx(EncounterEffectType.Experience, 30)),
                    Chance(ChanceResult.Success, 2, "Добрые слова.", Fx(EncounterEffectType.Experience, 15)),
                    Chance(ChanceResult.Failure, 2, "Туманное предсказание."),
                    Chance(ChanceResult.HugeFailure, 1, "Пока гадала — стащила подковы.", Fx(EncounterEffectType.Horseshoes, -3))),
                Direct("Отказаться", "Гадалка что-то бормочет вслед.")));

            EditorUtility.SetDirty(catalog);
            EncounterCatalog.SetInstance(catalog);
        }

        private static EncounterDefinition Encounter(string fileName, string title, string text, Color color, params EncounterChoice[] choices)
        {
            string path = $"{EncountersFolder}/{fileName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<EncounterDefinition>(path);
            if (existing != null) return existing;

            var encounter = ScriptableObject.CreateInstance<EncounterDefinition>();
            encounter.title = title;
            encounter.text = text;
            encounter.placeholderColor = color;
            encounter.choices = new List<EncounterChoice>(choices);
            AssetDatabase.CreateAsset(encounter, path);
            return encounter;
        }

        private static EncounterChoice Direct(string label, string result, int cost = 0, params EncounterEffect[] effects)
        {
            return new EncounterChoice { label = label, resultText = result, horseshoeCost = cost, effects = new List<EncounterEffect>(effects) };
        }

        // Вариант с риском: исход решает вытянутая карта удачи
        private static EncounterChoice Gamble(string label, params ChanceCard[] cards)
        {
            return new EncounterChoice { label = label, isGamble = true, chanceCards = new List<ChanceCard>(cards) };
        }

        private static ChanceCard Chance(ChanceResult result, int count, string text, params EncounterEffect[] effects)
        {
            return new ChanceCard { result = result, count = count, text = text, effects = new List<EncounterEffect>(effects) };
        }

        private static EncounterEffect Fx(EncounterEffectType type, int amount) => new EncounterEffect { type = type, amount = amount };

        // ---------------------------------------------------------------- Сцена карты

        private static void BuildMapScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCamera(null);
            CreateEventSystem();

            RectTransform area = CreateCanvas("Canvas");

            TextMeshProUGUI title = CreateText(area, "Title", "Карта похода", 90, TextAlignmentOptions.Center);
            SetAnchors(title.rectTransform, new Vector2(0f, 0.91f), new Vector2(1f, 0.985f));

            RectTransform view = CreateRect("MapView", area);
            SetAnchors(view, new Vector2(0f, 0.28f), new Vector2(1f, 0.905f));
            AddImage(view, new Color(0.1f, 0.12f, 0.1f, 0.9f));
            view.gameObject.AddComponent<RectMask2D>();

            RectTransform content = CreateRect("Content", view);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;
            content.sizeDelta = new Vector2(0f, 3400f);

            ScrollRect scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = view;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            RectTransform edgeRect = CreateRect("RoadTemplate", content);
            Image edge = edgeRect.gameObject.AddComponent<Image>();
            edge.raycastTarget = false;

            Button node = CreateButton(content, "TownTemplate", "Город", Color.white, 34);
            TextMeshProUGUI nodeLabel = node.GetComponentInChildren<TextMeshProUGUI>();
            SetAnchors(nodeLabel.rectTransform, new Vector2(-0.7f, -0.5f), new Vector2(1.7f, -0.02f));

            TextMeshProUGUI info = CreateText(area, "Info", "", 40, TextAlignmentOptions.Center);
            SetAnchors(info.rectTransform, new Vector2(0.04f, 0.16f), new Vector2(0.96f, 0.27f));

            Button travel = CreateButton(area, "TravelButton", "Сесть в дилижанс", AccentButtonColor, 72);
            SetAnchors((RectTransform)travel.transform, new Vector2(0.12f, 0.03f), new Vector2(0.88f, 0.14f));

            RunMapView view2 = new GameObject("RunMap").AddComponent<RunMapView>();
            Wire(view2,
                ("_title", title),
                ("_info", info),
                ("_scroll", scroll),
                ("_content", content),
                ("_nodeTemplate", node),
                ("_edgeTemplate", edge),
                ("_travelButton", travel),
                ("_travelScene", StagecoachSceneName),
                ("_menuScene", MenuSceneName));

            EditorSceneManager.SaveScene(scene, MapScenePath);
        }

        // ---------------------------------------------------------------- Сцена поездки

        private static void BuildStagecoachScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var fieldGo = new GameObject("PlayField");
            PlayField field = fieldGo.AddComponent<PlayField>();
            Wire(field, ("_size", FieldSize));

            CreateCamera(field);
            CreateGlobalLight();
            CreateEventSystem();

            RectTransform area = CreateCanvas("HUD");

            RectTransform bar = CreateRect("TopBar", area);
            SetAnchors(bar, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
            bar.sizeDelta = new Vector2(0f, 220f);
            AddImage(bar, BarColor);

            TextMeshProUGUI wheels = CreateText(bar, "Wheels", "Колёса: 3 / 3", 44, TextAlignmentOptions.MidlineLeft);
            SetAnchors(wheels.rectTransform, new Vector2(0.03f, 0.5f), new Vector2(0.5f, 0.95f));
            TextMeshProUGUI horseshoes = CreateText(bar, "Horseshoes", "Подковы: 0", 44, TextAlignmentOptions.MidlineRight);
            SetAnchors(horseshoes.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.97f, 0.95f));
            TextMeshProUGUI biome = CreateText(bar, "Biome", "Лес", 40, TextAlignmentOptions.Center);
            SetAnchors(biome.rectTransform, new Vector2(0.3f, 0.5f), new Vector2(0.7f, 0.95f));

            Slider progress = CreateSlider(bar, "Progress", new Color(1f, 0.82f, 0.24f));
            RectTransform progressRect = (RectTransform)progress.transform;
            SetAnchors(progressRect, new Vector2(0.03f, 0.1f), new Vector2(0.97f, 0.35f));

            TextMeshProUGUI hint = CreateText(area, "Hint", "Свайп — полоса, тап — прыжок", 40, TextAlignmentOptions.Center);
            SetAnchors(hint.rectTransform, new Vector2(0.05f, 0.02f), new Vector2(0.95f, 0.08f));

            EncounterPanel encounters = BuildEncounterPanel(area);

            StagecoachRun run = new GameObject("Stagecoach").AddComponent<StagecoachRun>();
            Wire(run,
                ("_encounterPanel", encounters),
                ("_arrivalScene", VillageSceneName),
                ("_wheelsText", wheels),
                ("_horseshoesText", horseshoes),
                ("_biomeText", biome),
                ("_hintText", hint),
                ("_progress", progress));

            EditorSceneManager.SaveScene(scene, StagecoachScenePath);
        }

        private static EncounterPanel BuildEncounterPanel(RectTransform area)
        {
            RectTransform holder = CreateRect("Encounter", area);
            Stretch(holder);
            EncounterPanel panel = holder.gameObject.AddComponent<EncounterPanel>();

            RectTransform root = CreateRect("Panel", holder);
            Stretch(root);
            AddImage(root, DimColor);

            RectTransform window = CreateRect("Card", root);
            SetAnchors(window, new Vector2(0.05f, 0.08f), new Vector2(0.95f, 0.92f));
            AddImage(window, new Color(0.22f, 0.18f, 0.14f, 0.98f));

            VerticalLayoutGroup layout = window.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(40, 40, 40, 40);
            layout.spacing = 22f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            TextMeshProUGUI title = CreateText(window, "Title", "Встреча", 76, TextAlignmentOptions.Center);
            AddLayoutHeight(title.gameObject, 110);

            RectTransform artRect = CreateRect("Art", window);
            Image art = artRect.gameObject.AddComponent<Image>();
            art.raycastTarget = false;
            AddLayoutHeight(artRect.gameObject, 300);

            TextMeshProUGUI text = CreateText(window, "Text", "", 42, TextAlignmentOptions.Center);
            AddLayoutHeight(text.gameObject, 220);

            // Варианты
            RectTransform choices = CreateRect("Choices", window);
            VerticalLayoutGroup choicesLayout = choices.gameObject.AddComponent<VerticalLayoutGroup>();
            choicesLayout.spacing = 18f;
            choicesLayout.childControlWidth = true;
            choicesLayout.childControlHeight = true;
            choicesLayout.childForceExpandWidth = true;
            choicesLayout.childForceExpandHeight = false;
            choices.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;

            Button choice = CreateButton(choices, "ChoiceTemplate", "Вариант", ButtonColor, 48);
            AddLayoutHeight(choice.gameObject, 140);

            // Карты удачи
            RectTransform chanceRoot = CreateRect("Chance", window);
            chanceRoot.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;
            TextMeshProUGUI chanceHint = CreateText(chanceRoot, "Hint", "Вытяните карту удачи", 44, TextAlignmentOptions.Center);
            SetAnchors(chanceHint.rectTransform, new Vector2(0f, 0.8f), new Vector2(1f, 1f));

            RectTransform cards = CreateRect("Cards", chanceRoot);
            SetAnchors(cards, new Vector2(0f, 0f), new Vector2(1f, 0.78f));
            GridLayoutGroup grid = cards.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(190f, 270f);
            grid.spacing = new Vector2(24f, 24f);
            grid.childAlignment = TextAnchor.MiddleCenter;

            Button card = CreateButton(cards, "CardTemplate", "?", new Color(0.3f, 0.2f, 0.45f), 90);

            // Итог
            RectTransform resultRoot = CreateRect("Result", window);
            resultRoot.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;
            TextMeshProUGUI resultText = CreateText(resultRoot, "Text", "", 46, TextAlignmentOptions.Center);
            SetAnchors(resultText.rectTransform, new Vector2(0f, 0.3f), new Vector2(1f, 1f));
            Button next = CreateButton(resultRoot, "ContinueButton", "Дальше в путь", AccentButtonColor, 56);
            SetAnchors((RectTransform)next.transform, new Vector2(0.1f, 0f), new Vector2(0.9f, 0.25f));

            Wire(panel,
                ("_root", root.gameObject),
                ("_title", title),
                ("_text", text),
                ("_art", art),
                ("_choicesContainer", choices),
                ("_choiceTemplate", choice),
                ("_chanceRoot", chanceRoot.gameObject),
                ("_chanceContainer", cards),
                ("_chanceCardTemplate", card),
                ("_resultRoot", resultRoot.gameObject),
                ("_resultText", resultText),
                ("_continueButton", next));

            return panel;
        }
    }
}
