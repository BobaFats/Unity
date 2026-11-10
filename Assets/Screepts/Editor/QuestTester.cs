using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Tanks2D.EditorTools
{
    // Быстрая проверка заданий: окно со списком заданий и кнопкой «Играть».
    // Игра запускается сразу в деревне нужного города, условия задания считаются выполненными —
    // не нужно проходить уровни и ехать на дилижансе, чтобы посмотреть одну сцену.
    // Меню: Tools > Vertical Shooter > Quest Tester.
    public class QuestTesterWindow : EditorWindow
    {
        private const string CatalogPath = "Assets/Resources/" + QuestCatalog.ResourcePath + ".asset";

        private Vector2 _scroll;

        [MenuItem("Tools/Vertical Shooter/Quest Tester", priority = 41)]
        public static void Open()
        {
            GetWindow<QuestTesterWindow>("Задания").Show();
        }

        private void OnGUI()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<QuestCatalog>(CatalogPath);
            if (catalog == null || catalog.Quests.Count == 0)
            {
                EditorGUILayout.HelpBox("Заданий пока нет. Выполните Tools > Vertical Shooter > Update Project (safe).", MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox("«Играть» запускает игру сразу в деревне нужного города. Условия задания (флаги) считаются выполненными, прогресс начинается с нуля.", MessageType.Info);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (QuestDefinition quest in catalog.Quests)
            {
                if (quest == null) continue;

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(quest.title, EditorStyles.boldLabel);

                        if (GUILayout.Button("Открыть", GUILayout.Width(80)))
                        {
                            Selection.activeObject = quest;
                            EditorGUIUtility.PingObject(quest);
                        }

                        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                        {
                            if (GUILayout.Button("Играть", GUILayout.Width(80))) QuestTestRunner.Play(quest);
                        }
                    }

                    string town = quest.town != null ? quest.town.displayName : "любой город";
                    string giver = string.IsNullOrEmpty(quest.giverNpcId) ? quest.giverName : $"житель «{quest.giverNpcId}»";
                    EditorGUILayout.LabelField("Где", $"{town}, здание «{quest.buildingId}», выдаёт {giver}");
                    EditorGUILayout.LabelField("Сцена", $"{quest.lines.Count} реплик и титров");
                    if (quest.requiredFlags.Count > 0) EditorGUILayout.LabelField("Нужны флаги", string.Join(", ", quest.requiredFlags));
                    if (quest.blockingFlags.Count > 0) EditorGUILayout.LabelField("Мешают флаги", string.Join(", ", quest.blockingFlags));
                    if (quest.setFlags.Count > 0) EditorGUILayout.LabelField("Выставляет флаги", string.Join(", ", quest.setFlags));
                }
            }
            EditorGUILayout.EndScrollView();
        }
    }

    [InitializeOnLoad]
    public static class QuestTestRunner
    {
        private const string PendingKey = "VerticalShooter.QuestTest.Pending";
        private const string VillageScenePath = "Assets/Scenes/Vertical/Village.unity";
        private const string VillageSceneName = "Village";

        static QuestTestRunner()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        public static void Play(QuestDefinition quest)
        {
            if (quest == null || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            SessionState.SetString(PendingKey, quest.Id);
            EditorSceneManager.OpenScene(VillageScenePath);
            EditorApplication.isPlaying = true;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode) return;

            string id = SessionState.GetString(PendingKey, "");
            if (string.IsNullOrEmpty(id)) return;
            SessionState.EraseString(PendingKey);

            QuestDefinition quest = QuestCatalog.Instance != null ? QuestCatalog.Instance.Find(id) : null;
            if (quest == null) return;

            // Проверка задания не должна затирать сохранение настоящего забега
            SaveSystem.UseTemporaryFolder("QuestTesterSaves");

            if (!Prepare(quest)) Debug.LogWarning($"[Задания] Не удалось поставить героя в город задания «{quest.title}» — города нет в каталоге");
            SceneManager.LoadScene(VillageSceneName);
        }

        // Новый забег, в котором герой уже стоит в городе задания, а условия задания выполнены
        public static bool Prepare(QuestDefinition quest)
        {
            GameStats.ResetAll();
            bool placed = quest.town == null || PlaceHero(quest.town);
            foreach (string flag in quest.requiredFlags) GameStats.SetStoryFlag(flag, true);
            return placed;
        }

        // Ищет карту забега, на которой есть этот город, и ведёт героя к нему по дорогам от старта
        public static bool PlaceHero(TownDefinition town)
        {
            TownCatalog catalog = TownCatalog.Instance;
            if (catalog == null || town == null) return false;

            int firstSeed = Random.Range(1, 100000);
            for (int i = 0; i < 300; i++)
            {
                RunMap map = RunMapGenerator.Generate(firstSeed + i, catalog.Towns);

                var path = new List<MapNode>();
                if (!Walk(map, map.Get(0, 0), town.Id, path)) continue;

                for (int step = 1; step < path.Count; step++) map.TravelTo(path[step].index);

                GameStats.Map = map;
                GameStats.Mission = RunMap.FirstLevelOfRow(map.currentRow);
                return true;
            }

            return false;
        }

        private static bool Walk(RunMap map, MapNode node, string townId, List<MapNode> path)
        {
            if (node == null) return false;

            path.Add(node);
            if (node.townId == townId) return true;

            foreach (int next in node.next)
            {
                if (Walk(map, map.Get(node.row + 1, next), townId, path)) return true;
            }

            path.RemoveAt(path.Count - 1);
            return false;
        }
    }
}
