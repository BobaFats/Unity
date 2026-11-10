using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Tanks2D.EditorTools
{
    // Папка Assets/Art — «приёмная» для арта.
    //   • Файл назван как слот игры (Player.png, IconDamage.png, building_castle.png...) — подключается сам:
    //     сразу при импорте или по меню Tools > Vertical Shooter > Art > Apply Art From Assets-Art.
    //   • Что именно нужно, как называть файл и что уже подключено — в Assets/Art/ART_LIST.md (пишется этим скриптом).
    //   • Всё остальное лежит в Assets/Art/Inbox и ждёт, пока его разберут.
    public static class ArtImporter
    {
        public const string Root = "Assets/Art";
        public const string InboxFolder = Root + "/Inbox";
        public const string ListPath = Root + "/ART_LIST.md";

        private const string CatalogPath = "Assets/Resources/" + VisualCatalog.ResourcePath + ".asset";
        private const string TownsFolder = "Assets/Resources/Towns";

        public enum SlotKind { None, Catalog, TownBackground, Building, NpcPortrait, Encounter, WeaponSkin, WeaponSkinIcon, QuestGiver }

        // Куда встаёт файл с таким именем
        public struct Slot
        {
            public SlotKind kind;
            public VisualId visual;   // слот каталога
            public string town;       // имя ассета города (Town_05); null — все города
            public string key;        // id здания / жителя или имя ассета встречи, скина, задания

            public bool IsValid => kind != SlotKind.None;
        }

        public class Report
        {
            public readonly List<string> applied = new List<string>();
            public readonly List<string> problems = new List<string>();
            public readonly List<string> unknown = new List<string>();
        }

        // ---------------------------------------------------------------- Имя файла -> слот

        // Имя файла без расширения. Регистр не важен.
        //   Player, IconDamage...          слот каталога (VisualId)
        //   Town_05                        фон города
        //   building_castle                здание во всех городах;  Town_05_building_castle — только в одном
        //   npc_trader                     портрет жителя во всех городах;  Town_05_npc_trader — только в одном
        //   encounter_Bandits              картинка встречи в дороге
        //   skin_Golden, skin_Golden_icon  скин оружия и его иконка
        //   quest_CarlsonJumpsuit          портрет жителя, выдающего задание
        public static Slot Resolve(string fileName)
        {
            string name = (fileName ?? "").Trim();
            if (name.Length == 0) return default;

            // Сравниваем с именем значения, а не просто разбираем: «5» или «+5» — не слот
            if (Enum.TryParse(name, true, out VisualId visual) && SameKey(visual.ToString(), name))
            {
                return new Slot { kind = SlotKind.Catalog, visual = visual };
            }

            string town = null;
            const string townPrefix = "town_";
            if (name.StartsWith(townPrefix, StringComparison.OrdinalIgnoreCase))
            {
                int end = townPrefix.Length;
                while (end < name.Length && char.IsDigit(name[end])) end++;
                if (end == townPrefix.Length || end - townPrefix.Length > 4) return default;

                town = $"Town_{int.Parse(name.Substring(townPrefix.Length, end - townPrefix.Length)):00}";
                if (end == name.Length) return new Slot { kind = SlotKind.TownBackground, town = town };
                if (name[end] != '_') return default;

                name = name.Substring(end + 1);
                if (name.Equals("bg", StringComparison.OrdinalIgnoreCase) || name.Equals("background", StringComparison.OrdinalIgnoreCase))
                {
                    return new Slot { kind = SlotKind.TownBackground, town = town };
                }
            }

            if (TryPrefix(name, "building_", out string key)) return new Slot { kind = SlotKind.Building, town = town, key = key.ToLowerInvariant() };
            if (TryPrefix(name, "npc_", out key)) return new Slot { kind = SlotKind.NpcPortrait, town = town, key = key.ToLowerInvariant() };
            if (town != null) return default; // после города допустимы только фон, здание и житель

            if (TryPrefix(name, "encounter_", out key)) return new Slot { kind = SlotKind.Encounter, key = key };
            if (TryPrefix(name, "quest_", out key)) return new Slot { kind = SlotKind.QuestGiver, key = key };
            if (TryPrefix(name, "skin_", out key))
            {
                const string iconSuffix = "_icon";
                return key.Length > iconSuffix.Length && key.EndsWith(iconSuffix, StringComparison.OrdinalIgnoreCase)
                    ? new Slot { kind = SlotKind.WeaponSkinIcon, key = key.Substring(0, key.Length - iconSuffix.Length) }
                    : new Slot { kind = SlotKind.WeaponSkin, key = key };
            }

            return default;
        }

        private static bool TryPrefix(string name, string prefix, out string rest)
        {
            rest = null;
            if (name.Length <= prefix.Length || !name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;
            rest = name.Substring(prefix.Length);
            return true;
        }

        // ---------------------------------------------------------------- Подключение

        [MenuItem("Tools/Vertical Shooter/Art/Apply Art From Assets-Art", priority = 22)]
        public static void ApplyAllMenu()
        {
            Report report = ApplyAll();
            string message = $"Подключено: {report.applied.Count}\nНе подошло по имени: {report.unknown.Count}\nЗамечаний: {report.problems.Count}\n\nПодробности — в Console, список слотов — в {ListPath}";
            EditorUtility.DisplayDialog("Vertical Shooter", message, "OK");
        }

        // Тихий запуск после импорта файлов в Assets/Art
        internal static void ApplyAfterImport() => ApplyAll();

        public static Report ApplyAll()
        {
            var report = new Report();
            if (!AssetDatabase.IsValidFolder(Root))
            {
                return report;
            }

            var catalog = AssetDatabase.LoadAssetAtPath<VisualCatalog>(CatalogPath);
            List<TownDefinition> towns = LoadTowns();
            List<EncounterDefinition> encounters = LoadAll<EncounterDefinition>();
            List<WeaponSkin> skins = LoadAll<WeaponSkin>();
            List<QuestDefinition> quests = LoadAll<QuestDefinition>();

            // Картинки. Сначала общие для всех городов, потом «только для одного города» — они важнее
            var general = new List<(Slot slot, string path)>();
            var townOnly = new List<(Slot slot, string path)>();
            foreach (string path in FindPaths("t:Texture2D"))
            {
                Slot slot = Resolve(Path.GetFileNameWithoutExtension(path));
                if (slot.IsValid) (slot.town != null ? townOnly : general).Add((slot, path));
                else if (!path.StartsWith(InboxFolder + "/")) report.unknown.Add(path);
            }
            // Город со своей картинкой здания или жителя общую картинку не получает (иначе слоты перезаписывались бы при каждом запуске)
            var townOverrides = new HashSet<string>();
            foreach ((Slot slot, string _) in townOnly) townOverrides.Add(OverrideKey(slot.kind, slot.town, slot.key));
            general.AddRange(townOnly);

            var taken = new Dictionary<string, string>();
            foreach ((Slot slot, string path) in general)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null)
                {
                    report.problems.Add($"{path}: не спрайт — в настройках импорта поставьте Texture Type = Sprite (2D and UI)");
                    continue;
                }

                string slotKey = $"{slot.kind}/{slot.town}/{(slot.kind == SlotKind.Catalog ? slot.visual.ToString() : slot.key.ToLowerInvariant())}";
                if (taken.TryGetValue(slotKey, out string other)) report.problems.Add($"{path}: на тот же слот претендует {other} — оставьте один файл");
                taken[slotKey] = path;

                ApplySprite(slot, sprite, path, catalog, towns, encounters, skins, quests, townOverrides, report);
            }

            // Модели: префаб или FBX, названный как слот каталога, встаёт в поле «prefab» (важнее спрайта)
            foreach (string path in FindPaths("t:GameObject"))
            {
                Slot slot = Resolve(Path.GetFileNameWithoutExtension(path));
                if (slot.kind != SlotKind.Catalog || catalog == null || !catalog.TryGet(slot.visual, out VisualEntry entry)) continue;

                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model == null || entry.prefab == model) continue;

                entry.prefab = model;
                EditorUtility.SetDirty(catalog);
                report.applied.Add($"{path} -> каталог: {slot.visual} (модель)");
            }

            if (report.applied.Count > 0) AssetDatabase.SaveAssets();
            WriteList();

            if (report.applied.Count > 0 || report.problems.Count > 0)
            {
                var log = new StringBuilder($"[Арт] Подключено: {report.applied.Count}, замечаний: {report.problems.Count}, не подошло по имени: {report.unknown.Count}\n");
                foreach (string line in report.applied) log.AppendLine("  + " + line);
                foreach (string line in report.problems) log.AppendLine("  ! " + line);
                foreach (string line in report.unknown) log.AppendLine("  ? " + line + " — имя не совпало ни с одним слотом (см. " + ListPath + ")");
                Debug.Log(log.ToString());
            }

            return report;
        }

        private static string OverrideKey(SlotKind kind, string town, string key) => $"{kind}/{town}/{(key ?? "").ToLowerInvariant()}";

        private static void ApplySprite(Slot slot, Sprite sprite, string path, VisualCatalog catalog, List<TownDefinition> towns,
            List<EncounterDefinition> encounters, List<WeaponSkin> skins, List<QuestDefinition> quests, HashSet<string> townOverrides, Report report)
        {
            switch (slot.kind)
            {
                case SlotKind.Catalog:
                {
                    if (catalog == null || !catalog.TryGet(slot.visual, out VisualEntry entry))
                    {
                        report.problems.Add($"{path}: в каталоге нет слота {slot.visual} — выполните Tools > Vertical Shooter > Update Project (safe)");
                        return;
                    }
                    if (Set(ref entry.sprite, sprite, catalog)) report.applied.Add($"{path} -> каталог: {slot.visual}");
                    return;
                }

                case SlotKind.TownBackground:
                {
                    TownDefinition town = towns.Find(t => t.name == slot.town);
                    if (town == null) { report.problems.Add($"{path}: нет города {slot.town}"); return; }
                    if (Set(ref town.background, sprite, town)) report.applied.Add($"{path} -> фон города «{town.displayName}»");
                    return;
                }

                case SlotKind.Building:
                case SlotKind.NpcPortrait:
                {
                    int found = 0, changed = 0;
                    foreach (TownDefinition town in towns)
                    {
                        if (slot.town != null && town.name != slot.town) continue;
                        if (slot.town == null && townOverrides.Contains(OverrideKey(slot.kind, town.name, slot.key))) { found++; continue; }

                        foreach (BuildingDefinition building in town.buildings)
                        {
                            if (building == null) continue;

                            if (slot.kind == SlotKind.Building)
                            {
                                if (!SameKey(building.id, slot.key)) continue;
                                found++;
                                if (Set(ref building.sprite, sprite, town)) changed++;
                                continue;
                            }

                            foreach (NpcDefinition npc in building.npcs)
                            {
                                if (npc == null || !SameKey(npc.id, slot.key)) continue;
                                found++;
                                if (Set(ref npc.portrait, sprite, town)) changed++;
                            }
                        }
                    }

                    string what = slot.kind == SlotKind.Building ? "здания" : "жителя";
                    string where = slot.town != null ? $"в городе {slot.town}" : "в городах";
                    if (found == 0) report.problems.Add($"{path}: нет {what} с id «{slot.key}» {where}");
                    else if (changed > 0) report.applied.Add($"{path} -> {(slot.kind == SlotKind.Building ? "здание" : "портрет жителя")} «{slot.key}» ({changed} шт.)");
                    return;
                }

                case SlotKind.Encounter:
                {
                    EncounterDefinition encounter = encounters.Find(e => SameKey(e.name, slot.key));
                    if (encounter == null) { report.problems.Add($"{path}: нет встречи {slot.key}"); return; }
                    if (Set(ref encounter.art, sprite, encounter)) report.applied.Add($"{path} -> встреча «{encounter.title}»");
                    return;
                }

                case SlotKind.WeaponSkin:
                case SlotKind.WeaponSkinIcon:
                {
                    WeaponSkin skin = skins.Find(s => SameKey(s.name, slot.key));
                    if (skin == null) { report.problems.Add($"{path}: нет скина оружия {slot.key}"); return; }

                    bool changed = slot.kind == SlotKind.WeaponSkin ? Set(ref skin.sprite, sprite, skin) : Set(ref skin.icon, sprite, skin);
                    if (changed) report.applied.Add($"{path} -> скин «{skin.displayName}»{(slot.kind == SlotKind.WeaponSkinIcon ? " (иконка)" : "")}");
                    return;
                }

                case SlotKind.QuestGiver:
                {
                    QuestDefinition quest = quests.Find(q => SameKey(q.name, slot.key));
                    if (quest == null) { report.problems.Add($"{path}: нет задания {slot.key}"); return; }
                    if (Set(ref quest.giverPortrait, sprite, quest)) report.applied.Add($"{path} -> задание «{quest.title}»: портрет «{quest.giverName}»");
                    return;
                }
            }
        }

        private static bool Set(ref Sprite field, Sprite value, UnityEngine.Object owner)
        {
            if (field == value) return false;
            field = value;
            EditorUtility.SetDirty(owner);
            return true;
        }

        private static bool SameKey(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

        private static List<string> FindPaths(string filter)
        {
            var paths = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets(filter, new[] { Root }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!paths.Contains(path)) paths.Add(path);
            }
            paths.Sort(StringComparer.OrdinalIgnoreCase);
            return paths;
        }

        private static List<T> LoadAll<T>() where T : UnityEngine.Object
        {
            var result = new List<T>();
            foreach (string guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null) result.Add(asset);
            }
            result.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return result;
        }

        // Города в порядке номеров (Town_01, Town_02...)
        private static List<TownDefinition> LoadTowns()
        {
            var result = new List<TownDefinition>();
            if (!AssetDatabase.IsValidFolder(TownsFolder)) return result;

            foreach (string guid in AssetDatabase.FindAssets("t:TownDefinition", new[] { TownsFolder }))
            {
                var town = AssetDatabase.LoadAssetAtPath<TownDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (town != null) result.Add(town);
            }
            result.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return result;
        }

        // ---------------------------------------------------------------- Список арта

        private static readonly Dictionary<VisualId, string> CatalogNotes = new Dictionary<VisualId, string>
        {
            { VisualId.Background, "Фон поля боя" },
            { VisualId.Wall, "Стена" },
            { VisualId.Player, "Герой (вид сверху, смотрит вверх)" },
            { VisualId.Weapon, "Оружие героя (стволом вверх)" },
            { VisualId.Bullet, "Пуля" },
            { VisualId.EnemyPig, "Враг: свинья" },
            { VisualId.EnemyChicken, "Враг: курица" },
            { VisualId.Boss, "Босс" },
            { VisualId.Coin, "Монета (сейчас в игре не используется)" },
            { VisualId.IconDamage, "Навык: урон" },
            { VisualId.IconFireRate, "Навык: скорострельность" },
            { VisualId.IconReload, "Навык: перезарядка" },
            { VisualId.IconAmmo, "Навык: обойма" },
            { VisualId.IconWallHP, "Навык: стена (прочность и ремонт)" },
            { VisualId.Tower, "Башня-помощник" },
            { VisualId.IconTower, "Навык: новая башня" },
            { VisualId.IconMultiShot, "Навык: больше пуль" },
            { VisualId.IconSpread, "Навык: разброс" },
            { VisualId.BossShield, "Щит босса (полупрозрачный круг)" },
            { VisualId.ElementFire, "Стихия: огонь" },
            { VisualId.ElementIce, "Стихия: холод" },
            { VisualId.ElementWind, "Стихия: ветер" },
            { VisualId.ElementEarth, "Стихия: земля" },
            { VisualId.YogSothoth, "Мини-босс Йог-Сотот" },
            { VisualId.Tentacle, "Щупальце Йог-Сотота" },
            { VisualId.IconPierce, "Навык: пробивание" },
            { VisualId.IconExplosive, "Навык: взрывные пули" },
            { VisualId.IconTowerPower, "Навык: сила башен" },
            { VisualId.Npc, "Портрет жителя по умолчанию" },
            { VisualId.Stagecoach, "Дилижанс (вид сверху)" },
            { VisualId.RoadLog, "Препятствие: бревно (перепрыгивается)" },
            { VisualId.RoadHole, "Препятствие: яма (перепрыгивается)" },
            { VisualId.RoadRock, "Препятствие: камень" },
            { VisualId.RoadTree, "Препятствие: дерево" },
            { VisualId.RoadGap, "Пролом во всю дорогу (перепрыгивается)" },
            { VisualId.Horseshoe, "Подкова" },
            { VisualId.Traveler, "Путник у дороги (встреча)" },
            { VisualId.Signpost, "Указатель на развилке" }
        };

        private enum CatalogGroup { Battle, Icons, Road }

        private static CatalogGroup GroupOf(VisualId id)
        {
            string name = id.ToString();
            if (name.StartsWith("Icon") || name.StartsWith("Element") || id == VisualId.Npc || id == VisualId.Coin) return CatalogGroup.Icons;
            if (name.StartsWith("Road") || id == VisualId.Stagecoach || id == VisualId.Horseshoe || id == VisualId.Traveler || id == VisualId.Signpost) return CatalogGroup.Road;
            return CatalogGroup.Battle;
        }

        // Рекомендуемый размер картинки: поле боя 9×16 единиц = 1080×1920 px, мелкие объекты — с двойным запасом чёткости
        public static Vector2Int RecommendedPixels(VisualId id, Vector2 worldSize)
        {
            if (GroupOf(id) == CatalogGroup.Icons) return new Vector2Int(256, 256);

            float scale = Mathf.Max(worldSize.x, worldSize.y) >= 4f ? 120f : 240f;
            return new Vector2Int(RoundTo8(worldSize.x * scale), RoundTo8(worldSize.y * scale));
        }

        private static int RoundTo8(float value) => Mathf.Max(32, Mathf.RoundToInt(value / 8f) * 8);

        private const string Done = "✅";
        private const string Todo = "⬜";

        public static void WriteList()
        {
            if (!AssetDatabase.IsValidFolder(Root)) return;

            var catalog = AssetDatabase.LoadAssetAtPath<VisualCatalog>(CatalogPath);
            List<TownDefinition> towns = LoadTowns();
            var text = new StringBuilder();

            text.AppendLine("# Список арта");
            text.AppendLine();
            text.AppendLine("> Файл пишется автоматически (Tools > Vertical Shooter > Update Project (safe) или Art > Apply Art From Assets-Art). Руками не править.");
            text.AppendLine();
            text.AppendLine("Положите картинку с именем из колонки «Файл» в любую подпапку `Assets/Art` — она подключится сама.");
            text.AppendLine($"{Done} — арт подключён, {Todo} — пока заглушка. Размеры — ориентир: картинка вписывается в нужное место с сохранением пропорций.");
            text.AppendLine("Формат — PNG с прозрачным фоном (фонам прозрачность не нужна).");
            text.AppendLine();

            if (catalog != null)
            {
                AppendCatalog(text, catalog, CatalogGroup.Battle, "Бой");
                AppendCatalog(text, catalog, CatalogGroup.Icons, "Иконки и портреты");
                AppendCatalog(text, catalog, CatalogGroup.Road, "Дорога (дилижанс)");
            }

            AppendTowns(text, towns);
            AppendAssets(text, "Встречи в дороге", "encounter_", LoadAll<EncounterDefinition>(), e => e.title, e => e.art != null, "896×296");
            AppendSkins(text, LoadAll<WeaponSkin>());
            AppendAssets(text, "Задания: портрет того, кто выдаёт", "quest_", LoadAll<QuestDefinition>(),
                q => $"{q.giverName} — «{q.title}»", q => q.giverPortrait != null || !string.IsNullOrEmpty(q.giverNpcId), "256×256");

            string content = text.ToString().Replace("\r\n", "\n");
            if (File.Exists(ListPath) && File.ReadAllText(ListPath).Replace("\r\n", "\n") == content) return;

            File.WriteAllText(ListPath, content, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(ListPath);
        }

        private static void AppendCatalog(StringBuilder text, VisualCatalog catalog, CatalogGroup group, string title)
        {
            text.AppendLine($"## {title}");
            text.AppendLine();
            text.AppendLine("| Файл | Что это | Размер, px | Статус |");
            text.AppendLine("|---|---|---|---|");

            foreach (VisualEntry entry in catalog.Entries)
            {
                if (GroupOf(entry.id) != group) continue;

                Vector2Int size = RecommendedPixels(entry.id, entry.size);
                string note = CatalogNotes.TryGetValue(entry.id, out string known) ? known : entry.id.ToString();
                string status = entry.prefab != null ? Done + " модель" : entry.sprite != null ? Done : Todo;
                text.AppendLine($"| `{entry.id}.png` | {note} | {size.x}×{size.y} | {status} |");
            }
            text.AppendLine();
        }

        private static void AppendTowns(StringBuilder text, List<TownDefinition> towns)
        {
            if (towns.Count == 0) return;

            // Панорама города на экране: высота около 1080 px, ширина — по пропорциям города
            const int panoramaHeight = 1080;
            TownDefinition sample = towns[0];
            int panoramaWidth = RoundTo8(panoramaHeight * sample.panoramaAspect);

            text.AppendLine("## Города: здания");
            text.AppendLine();
            text.AppendLine("Одна картинка на здание — сразу для всех городов. Своя картинка для одного города: `Town_05_building_castle.png`.");
            text.AppendLine();
            text.AppendLine("| Файл | Что это | Размер, px | Статус |");
            text.AppendLine("|---|---|---|---|");
            foreach (BuildingDefinition building in sample.buildings)
            {
                if (building == null) continue;
                int with = towns.FindAll(t => t.buildings.Exists(b => b != null && b.id == building.id && b.sprite != null)).Count;
                string size = $"{RoundTo8(building.area.width * panoramaWidth)}×{RoundTo8(building.area.height * panoramaHeight)}";
                text.AppendLine($"| `building_{building.id}.png` | {building.displayName} | {size} | {Status(with, towns.Count)} |");
            }
            text.AppendLine();

            text.AppendLine("## Города: портреты жителей");
            text.AppendLine();
            text.AppendLine("Один портрет на жителя — для всех городов. Свой портрет для одного города: `Town_05_npc_trader.png`.");
            text.AppendLine();
            text.AppendLine("| Файл | Кто это | Размер, px | Статус |");
            text.AppendLine("|---|---|---|---|");
            foreach (BuildingDefinition building in sample.buildings)
            {
                if (building == null) continue;
                foreach (NpcDefinition npc in building.npcs)
                {
                    if (npc == null) continue;
                    int with = towns.FindAll(t => t.buildings.Exists(b => b != null && b.npcs.Exists(n => n != null && n.id == npc.id && n.portrait != null))).Count;
                    text.AppendLine($"| `npc_{npc.id}.png` | {npc.displayName} ({building.displayName}) | 256×256 | {Status(with, towns.Count)} |");
                }
            }
            text.AppendLine();

            text.AppendLine("## Города: фоны панорамы");
            text.AppendLine();
            text.AppendLine("Фон без зданий: небо, земля, вода. Здания ставятся поверх отдельными картинками.");
            text.AppendLine();
            text.AppendLine("| Файл | Город | Размер, px | Статус |");
            text.AppendLine("|---|---|---|---|");
            foreach (TownDefinition town in towns)
            {
                string story = town.fixedMapRow >= 0 ? $" (сюжетный, ряд карты {town.fixedMapRow + 1})" : "";
                text.AppendLine($"| `{town.name}.png` | {town.displayName}{story} | {RoundTo8(panoramaHeight * town.panoramaAspect)}×{panoramaHeight} | {(town.background != null ? Done : Todo)} |");
            }
            text.AppendLine();
        }

        private static void AppendAssets<T>(StringBuilder text, string title, string prefix, List<T> assets, Func<T, string> describe, Func<T, bool> hasArt, string size)
            where T : UnityEngine.Object
        {
            if (assets.Count == 0) return;

            text.AppendLine($"## {title}");
            text.AppendLine();
            text.AppendLine("| Файл | Что это | Размер, px | Статус |");
            text.AppendLine("|---|---|---|---|");
            foreach (T asset in assets) text.AppendLine($"| `{prefix}{asset.name}.png` | {describe(asset)} | {size} | {(hasArt(asset) ? Done : Todo)} |");
            text.AppendLine();
        }

        private static void AppendSkins(StringBuilder text, List<WeaponSkin> skins)
        {
            if (skins.Count == 0) return;

            text.AppendLine("## Скины оружия");
            text.AppendLine();
            text.AppendLine("| Файл | Что это | Размер, px | Статус |");
            text.AppendLine("|---|---|---|---|");
            foreach (WeaponSkin skin in skins)
            {
                text.AppendLine($"| `skin_{skin.name}.png` | Оружие «{skin.displayName}» (стволом вверх) | 64×264 | {(skin.sprite != null || skin.prefab != null ? Done : Todo)} |");
                text.AppendLine($"| `skin_{skin.name}_icon.png` | Иконка «{skin.displayName}» для лобби | 128×128 | {(skin.icon != null ? Done : Todo)} |");
            }
            text.AppendLine();
        }

        private static string Status(int with, int total)
        {
            if (with <= 0) return Todo;
            return with >= total ? Done : $"{Done} в {with} из {total} городов";
        }
    }

    // Картинки, попавшие в Assets/Art, сразу импортируются как спрайты и подключаются по имени
    internal class ArtPostprocessor : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(ArtImporter.Root + "/")) return;
            // Только первый импорт: настройки, поправленные вручную, не затираем
            if (!assetImporter.importSettingsMissing) return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
        }

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (Application.isBatchMode) return; // в командной строке арт подключает Update Project
            if (!TouchesArt(imported) && !TouchesArt(moved)) return;

            // Подключаем после импорта: внутри постпроцессора сохранять ассеты нельзя
            EditorApplication.delayCall -= ArtImporter.ApplyAfterImport;
            EditorApplication.delayCall += ArtImporter.ApplyAfterImport;
        }

        private static bool TouchesArt(string[] paths)
        {
            foreach (string path in paths)
            {
                if (!path.StartsWith(ArtImporter.Root + "/")) continue;

                string extension = Path.GetExtension(path).ToLowerInvariant();
                if (extension == ".png" || extension == ".jpg" || extension == ".jpeg" || extension == ".psd" || extension == ".tga"
                    || extension == ".bmp" || extension == ".tif" || extension == ".tiff" || extension == ".gif"
                    || extension == ".prefab" || extension == ".fbx") return true;
            }
            return false;
        }
    }
}
