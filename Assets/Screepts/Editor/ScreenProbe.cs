using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Tanks2D.EditorTools
{
    // Снимки экранов без участия человека: запускает Play Mode, проходит сценарий и сохраняет PNG 1080x1920.
    // Нужен, чтобы проверять, как выглядит интерфейс, из командной строки (графика нужна — без -nographics):
    //   Unity.exe -batchmode -projectPath <папка> -executeMethod Tanks2D.EditorTools.ScreenProbe.RunQuestBatch -probeOut <папка для PNG>
    // Сценарий: деревня города задания -> здание -> вся сцена задания по репликам -> итог.
    // Необязательно: -probeQuest <id задания> (по умолчанию первое в каталоге).
    [InitializeOnLoad]
    public static class ScreenProbe
    {
        private const string ActiveKey = "VerticalShooter.ScreenProbe.Active";
        private const string ScenarioKey = "VerticalShooter.ScreenProbe.Scenario";
        private const string VillageScenePath = "Assets/Scenes/Vertical/Village.unity";
        private const int Width = 1080;
        private const int Height = 1920;

        private static readonly List<(float wait, Action action)> _steps = new List<(float, Action)>();
        private static int _step;
        private static double _stepStart;
        private static double _waitStart;
        private static string _outFolder;
        private static Camera _camera;
        private static RenderTexture _target;
        private static int _shots;

        static ScreenProbe()
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;

            if (EditorApplication.isPlaying) EditorApplication.delayCall += Begin;
            else EditorApplication.update += WaitAndEnterPlayMode;
        }

        public static void RunQuestBatch() => Start("quest", VillageScenePath);

        // Итоги похода, окно «Наследие» и главное меню
        public static void RunMetaBatch() => Start("meta", "Assets/Scenes/Vertical/Menu.unity");

        private static void Start(string scenario, string scenePath)
        {
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetString(ScenarioKey, scenario);
            EditorSceneManager.OpenScene(scenePath);
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            _waitStart = EditorApplication.timeSinceStartup;
            EditorApplication.update -= WaitAndEnterPlayMode;
            EditorApplication.update += WaitAndEnterPlayMode;
        }

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
            if (state == PlayModeStateChange.EnteredPlayMode) Begin();
        }

        private static string Argument(string name, string fallback)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == name) return args[i + 1];
            return fallback;
        }

        // ---------------------------------------------------------------- Сценарий задания

        private static void Begin()
        {
            SaveSystem.UseTemporaryFolder("ProbeSaves");
            _outFolder = Argument("-probeOut", "Logs/ScreenProbe");
            Directory.CreateDirectory(_outFolder);
            _steps.Clear();
            _step = 0;
            _shots = 0;

            if (SessionState.GetString(ScenarioKey, "quest") == "meta")
            {
                BuildMetaSteps();
                StartTicking();
                return;
            }

            QuestCatalog catalog = QuestCatalog.Instance;
            string questId = Argument("-probeQuest", "");
            QuestDefinition quest = catalog == null ? null
                : string.IsNullOrEmpty(questId) ? (catalog.Quests.Count > 0 ? catalog.Quests[0] : null) : catalog.Find(questId);

            if (quest == null)
            {
                Debug.LogError("[PROBE] Задание не найдено");
                Finish(false);
                return;
            }

            QuestTestRunner.Prepare(quest);
            SceneManager.LoadScene("Village");

            Add(1.5f, SetUpCapture);
            Add(0.6f, () => Shot("village"));
            Add(0.1f, () =>
            {
                VillageController village = UnityEngine.Object.FindAnyObjectByType<VillageController>();
                village.OpenBuilding(village.Town.buildings.FindIndex(b => b.id == quest.buildingId));
            });
            Add(0.6f, () => Shot("building"));
            Add(0.1f, () =>
            {
                // Текст сразу целиком: снимаем готовые реплики, а не середину «печати»
                DialoguePanel dialogue = UnityEngine.Object.FindAnyObjectByType<DialoguePanel>();
                typeof(DialoguePanel).GetField("_charactersPerSecond", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(dialogue, 100000f);
                UnityEngine.Object.FindAnyObjectByType<VillageController>().PlayQuest(quest);
            });

            for (int i = 0; i < quest.lines.Count; i++)
            {
                int index = i;
                Add(0.5f, () => Shot($"line_{index + 1:00}"));
                Add(0.05f, NextLine);
            }

            Add(0.5f, () => Shot("result"));
            Add(0.05f, NextLine);
            Add(0.6f, () => Shot("building_after"));
            Add(0.1f, () => UnityEngine.Object.FindAnyObjectByType<VillageController>().CloseBuilding());
            Add(0.6f, () => Shot("village_after"));
            Add(0.1f, () => Finish(true));

            StartTicking();
        }

        private static void StartTicking()
        {
            _stepStart = EditorApplication.timeSinceStartup;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private static void BuildMetaSteps()
        {
            // Пример похода: 7 уровней, полтысячи убийств
            GameStats.ResetAll();
            GameStats.Mission = 8;
            GameStats.BossesDefeated = 7;
            GameStats.RunKills = 523;
            GameStats.RunDefeats = 2;
            GameStats.RunPlayTime = 1834f;
            MetaProgress.AddGlory(150);
            RunSummary.Finish(false);

            Add(1.5f, SetUpCapture);
            Add(0.6f, () => Shot("run_end"));
            Add(0.1f, () => UnityEngine.Object.FindAnyObjectByType<LegacyPanel>().Open());
            Add(0.6f, () => Shot("legacy"));
            Add(0.1f, () => UnityEngine.Object.FindAnyObjectByType<LegacyPanel>().Buy(0));
            Add(0.6f, () => Shot("legacy_bought"));
            Add(0.1f, () => SceneManager.LoadScene("Menu"));
            Add(1.5f, SetUpCapture);
            Add(0.6f, () => Shot("menu"));
            Add(0.1f, () => Finish(true));
        }

        private static void NextLine()
        {
            DialoguePanel dialogue = UnityEngine.Object.FindAnyObjectByType<DialoguePanel>();
            if (dialogue != null) dialogue.Next();
        }

        private static void Add(float wait, Action action) => _steps.Add((wait, action));

        private static void Tick()
        {
            if (!EditorApplication.isPlaying || _step >= _steps.Count) return;
            if (EditorApplication.timeSinceStartup - _stepStart < _steps[_step].wait) return;

            Action action = _steps[_step].action;
            _step++;
            _stepStart = EditorApplication.timeSinceStartup;

            try
            {
                action();
            }
            catch (Exception exception)
            {
                Debug.LogError($"[PROBE] Шаг {_step}: {exception}");
                Finish(false);
            }
        }

        // ---------------------------------------------------------------- Съёмка

        // Интерфейс рисуется поверх экрана и в текстуру сам не попадает — на время съёмки отдаём его камере
        private static void SetUpCapture()
        {
            _camera = Camera.main;
            _target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            _camera.targetTexture = _target;

            foreach (Canvas canvas in UnityEngine.Object.FindObjectsByType<Canvas>())
            {
                if (!canvas.isRootCanvas || canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = _camera;
                canvas.planeDistance = 5f;
            }
        }

        private static void Shot(string name)
        {
            if (_camera == null || _target == null) throw new InvalidOperationException("съёмка не подготовлена");

            Canvas.ForceUpdateCanvases();
            _camera.Render();

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = _target;
            var texture = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;

            _shots++;
            string path = Path.Combine(_outFolder, $"{_shots:00}_{name}.png");
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.Destroy(texture);
            Debug.Log($"[PROBE] {path}");
        }

        private static void Finish(bool passed)
        {
            EditorApplication.update -= Tick;
            SessionState.SetBool(ActiveKey, false);
            GameStats.ResetAll();

            Debug.Log($"[PROBE] {(passed ? "DONE" : "FAILED")}: снимков {_shots}, папка {_outFolder}");
            EditorApplication.isPlaying = false;
            if (Application.isBatchMode) EditorApplication.delayCall += () => EditorApplication.Exit(passed ? 0 : 1);
        }
    }
}
