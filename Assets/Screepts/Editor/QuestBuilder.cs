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
    // Задания: ассеты заданий, их каталог и то, что нужно заданиям в сцене деревни
    // (карточка титров в диалоге, метки «!» на зданиях и жителях).
    // Вызывается из ApplyUpgrades (Update Project (safe)); существующие задания и сцена не пересоздаются.
    public static partial class VerticalProjectBuilder
    {
        private const string QuestsFolder = ResourcesFolder + "/Quests";
        private const string QuestCatalogPath = ResourcesFolder + "/" + QuestCatalog.ResourcePath + ".asset";

        // Флаги мира, которые выставляют задания. Имена — в одном месте, чтобы не ошибиться буквой
        public const string FlagTraderKidnapped = "torgash_kidnapped";
        public const string FlagRomaInJumpsuit = "roma_in_jumpsuit";

        private static void EnsureQuests()
        {
            EnsureFolder(QuestsFolder);

            var catalog = AssetDatabase.LoadAssetAtPath<QuestCatalog>(QuestCatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<QuestCatalog>();
                AssetDatabase.CreateAsset(catalog, QuestCatalogPath);
            }

            catalog.Add(CarlsonJumpsuitQuest());

            EditorUtility.SetDirty(catalog);
            QuestCatalog.SetInstance(catalog);

            UpgradeVillageForQuests();
        }

        // «Комбинезон Карлсона»: Новый Сарай, рынок. Рома меняется одеждой с торгашом,
        // и разгневанные жители Старого Сарая похищают торгаша вместо Ромы.
        private static QuestDefinition CarlsonJumpsuitQuest()
        {
            string path = $"{QuestsFolder}/CarlsonJumpsuit.asset";
            var existing = AssetDatabase.LoadAssetAtPath<QuestDefinition>(path);
            if (existing != null) return existing;

            const string trader = "Торгаш";
            const string roma = "Рома";

            var quest = ScriptableObject.CreateInstance<QuestDefinition>();
            quest.title = "Комбинезон Карлсона";
            quest.description = "Жители Старого Сарая ищут Рому по приметам и одежде. Рома меняется одеждой с торгашом Нового Сарая — и вместо Ромы похищают торгаша.";
            quest.town = AssetDatabase.LoadAssetAtPath<TownDefinition>($"{TownsFolder}/Town_02.asset");
            quest.buildingId = "market";
            quest.giverName = trader;
            quest.giverColor = new Color(0.9f, 0.62f, 0.3f);

            quest.lines = new List<DialogueLine>
            {
                Narration("Из-за того что Рома напортачил в прошлом задании, жители Старого Сарая ищут Рому по всей местности — вплоть до Нового Сарая."),
                Narration("Тем временем в Новом Сарае..."),
                Speech(trader, VisualId.Npc, false, "<i>(с акцентом)</i> Ого, ничего себе, какая у вас одежда, господит!"),
                Speech(roma, VisualId.Player, true, "Нравится, да? Обменяю на твой обосранный комбинезон."),
                Speech(trader, VisualId.Npc, false, "<i>(в мыслях: ничего себе удача!)</i>\nСэр, только с вашей доплатой. Это комбинезон самого... Ээээ... Карлсона! Да, Карлсона!"),
                Speech(roma, VisualId.Player, true, "Ладно, по рукам."),
                Narration("Происходит обмен.\nТорговец переодевается."),
                Narration("Спустя 2 часа..."),
                Narration("Разгневанные жители Старого Сарая спрашивают дурно пахнущего фермера по приметам и одежде Ромы."),
                Speech("Житель Старого Сарая", VisualId.Npc, false, "Фууу, ну и вонище! Ты что, обосрался?"),
                Speech("Переодетый Рома", VisualId.Player, true, "Дважды. Парень, про которого вы спрашиваете, бродит на рынке."),
                Narration("Занавес.\nТорговца похищают разбушевавшиеся жители.")
            };

            quest.resultText = "Погоня сбита со следа: вместо Ромы увели торгаша.";
            quest.setFlags = new List<string> { FlagTraderKidnapped, FlagRomaInJumpsuit };

            AssetDatabase.CreateAsset(quest, path);
            return quest;
        }

        private static DialogueLine Speech(string speaker, VisualId portrait, bool rightSide, string text)
        {
            return new DialogueLine { kind = DialogueLineKind.Speech, speaker = speaker, portrait = portrait, rightSide = rightSide, text = text };
        }

        private static DialogueLine Narration(string text)
        {
            return new DialogueLine { kind = DialogueLineKind.Narration, text = text };
        }

        // ---------------------------------------------------------------- Сцена деревни

        private static void UpgradeVillageForQuests()
        {
            if (!File.Exists(VillageScenePath)) return;

            Scene scene = EditorSceneManager.OpenScene(VillageScenePath);
            VillageController village = Object.FindAnyObjectByType<VillageController>();
            if (village == null) return;

            var so = new SerializedObject(village);
            bool changed = false;

            changed |= EnsureNarrationCard(so.FindProperty("_dialogue").objectReferenceValue as DialoguePanel);
            // Метка у здания — в правом верхнем углу, у жителя — справа по центру
            changed |= EnsureQuestMarker(so.FindProperty("_buildingTemplate").objectReferenceValue as Button, new Vector2(1f, 1f), new Vector2(0.75f, 0.75f), Vector2.zero);
            changed |= EnsureQuestMarker(so.FindProperty("_npcTemplate").objectReferenceValue as Button, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-30f, 0f));

            if (changed) EditorSceneManager.SaveScene(scene);
        }

        // Карточка титров («Спустя 2 часа...») поверх окна реплик
        private static bool EnsureNarrationCard(DialoguePanel dialogue)
        {
            if (dialogue == null) return false;

            var so = new SerializedObject(dialogue);
            if (so.FindProperty("_narrationRoot").objectReferenceValue != null) return false;

            var root = so.FindProperty("_root").objectReferenceValue as GameObject;
            if (root == null) return false;
            Transform box = root.transform.Find("Box");

            RectTransform card = CreateRect("Narration", root.transform);
            Stretch(card);
            Image background = card.gameObject.AddComponent<Image>();
            background.color = new Color(0.02f, 0.02f, 0.04f, 0.95f);
            background.raycastTarget = false;

            TextMeshProUGUI text = CreateText(card, "Text", "Спустя 2 часа...", 64, TextAlignmentOptions.Center);
            SetAnchors(text.rectTransform, new Vector2(0.08f, 0.28f), new Vector2(0.92f, 0.74f));
            text.fontStyle = FontStyles.Italic;

            TextMeshProUGUI hint = CreateText(card, "Hint", "Нажмите, чтобы продолжить", 30, TextAlignmentOptions.Center);
            SetAnchors(hint.rectTransform, new Vector2(0.1f, 0.1f), new Vector2(0.9f, 0.14f));
            hint.color = new Color(1f, 1f, 1f, 0.5f);

            card.gameObject.SetActive(false);

            Wire(dialogue,
                ("_narrationRoot", card.gameObject),
                ("_narrationText", text),
                ("_speechRoot", box != null ? box.gameObject : null));
            return true;
        }

        // Метка «!» в шаблоне кнопки: VillageController включает её там, где есть задание
        private static bool EnsureQuestMarker(Button template, Vector2 anchor, Vector2 pivot, Vector2 position)
        {
            if (template == null || template.transform.Find(VillageController.QuestMarkerName) != null) return false;

            const float size = 92f;

            RectTransform marker = CreateRect(VillageController.QuestMarkerName, template.transform);
            marker.anchorMin = anchor;
            marker.anchorMax = anchor;
            marker.pivot = pivot;
            marker.anchoredPosition = position;
            marker.sizeDelta = new Vector2(size, size);

            Image image = marker.gameObject.AddComponent<Image>();
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{PlaceholderFolder}/{PlaceholderShape.Circle}.png");
            image.color = new Color(1f, 0.82f, 0.2f);
            image.raycastTarget = false;

            TextMeshProUGUI mark = CreateText(marker, "Text", "!", 72, TextAlignmentOptions.Center);
            Stretch(mark.rectTransform);
            mark.color = new Color(0.1f, 0.08f, 0.05f);
            mark.fontStyle = FontStyles.Bold;

            marker.gameObject.SetActive(false);
            return true;
        }
    }
}
