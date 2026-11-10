using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Tanks2D
{
    // Сохранение забега на диск: Application.persistentDataPath/run.json.
    // Сохраняемся «между экранами» — при входе в лобби, деревню и на карту похода.
    // Бой и поездка не сохраняются: если выйти посреди них, игра продолжится с последнего экрана,
    // а бой начнётся заново (поражение тоже откатывает прогресс к сохранению перед боем — «повтор уровня»).
    public static class SaveSystem
    {
        public const string RunFileName = "run.json";
        public const string MetaFileName = "meta.json";

        // Для автотестов: свои файлы, чтобы не трогать сохранения игрока
        public static string FolderOverride { get; set; }

        public static string Folder => string.IsNullOrEmpty(FolderOverride) ? Application.persistentDataPath : FolderOverride;
        public static string RunPath => Path.Combine(Folder, RunFileName);
        public static string MetaPath => Path.Combine(Folder, MetaFileName);

        public static bool HasRun => File.Exists(RunPath);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad() => FolderOverride = null;

        // Автотесты и инструменты: пустая временная папка вместо сохранений игрока (до выхода из Play Mode)
        public static void UseTemporaryFolder(string name)
        {
            FolderOverride = Path.Combine(Application.temporaryCachePath, name);
            if (Directory.Exists(FolderOverride)) Directory.Delete(FolderOverride, true);
            Directory.CreateDirectory(FolderOverride);
            MetaProgress.Reload();
        }

        // ---------------------------------------------------------------- Забег

        public static void SaveRun(string scene)
        {
            try
            {
                RunSaveData data = Capture(scene);
                WriteAtomic(RunPath, JsonUtility.ToJson(data, true));
            }
            catch (Exception exception)
            {
                Debug.LogError($"[Сохранение] Не удалось сохранить забег: {exception.Message}");
            }
        }

        // Загрузить забег в GameStats. Возвращает сцену, где его сохранили (null — сохранения нет или файл испорчен)
        public static string LoadRun()
        {
            RunSaveData data = ReadRun();
            if (data == null) return null;

            Restore(data);
            return string.IsNullOrEmpty(data.scene) ? "Lobby" : data.scene;
        }

        public static RunSaveData ReadRun()
        {
            if (!HasRun) return null;

            try
            {
                var data = JsonUtility.FromJson<RunSaveData>(File.ReadAllText(RunPath));
                return data != null && data.version > 0 ? data : null;
            }
            catch (Exception exception)
            {
                Debug.LogError($"[Сохранение] Файл забега испорчен: {exception.Message}");
                return null;
            }
        }

        public static void DeleteRun()
        {
            try
            {
                if (File.Exists(RunPath)) File.Delete(RunPath);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[Сохранение] Не удалось удалить забег: {exception.Message}");
            }
        }

        // Бой был брошен (выход в лобби/меню, поражение): всё, что получено в нём, отменяется —
        // прогресс возвращается к сохранению перед боем. Статистика (убийства, время, поражения) сохраняется.
        // true — откат был
        public static bool RevertUnfinishedBattle()
        {
            if (!GameStats.InBattle) return false;
            GameStats.InBattle = false;

            RunSaveData data = ReadRun();
            if (data == null || data.runId != GameStats.RunId) return false;

            int kills = GameStats.RunKills;
            int defeats = GameStats.RunDefeats;
            float time = GameStats.RunPlayTime;

            Restore(data);

            GameStats.RunKills = kills;
            GameStats.RunDefeats = defeats;
            GameStats.RunPlayTime = time;
            return true;
        }

        // ---------------------------------------------------------------- Снимок

        public static RunSaveData Capture(string scene)
        {
            var data = new RunSaveData
            {
                scene = scene,
                savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                bulletDamage = GameStats.BulletDamage,
                fireDelay = GameStats.FireDelay,
                reloadTime = GameStats.ReloadTime,
                maxAmmo = GameStats.MaxAmmo,
                wallMaxHP = GameStats.WallMaxHP,
                bulletCount = GameStats.BulletCount,
                spreadAngle = GameStats.SpreadAngle,
                towerCount = GameStats.TowerCount,
                pierce = GameStats.Pierce,
                explosionLevel = GameStats.ExplosionLevel,
                towerPowerLevel = GameStats.TowerPowerLevel,
                level = GameStats.Level,
                experience = GameStats.Experience,
                weaponSkinId = GameStats.WeaponSkinId,
                mission = GameStats.Mission,
                bossesDefeated = GameStats.BossesDefeated,
                campaignCompleted = GameStats.CampaignCompleted,
                currentTownId = GameStats.CurrentTownId,
                map = GameStats.Map,
                horseshoes = GameStats.Horseshoes,
                nextBattleWallDamage = GameStats.NextBattleWallDamage,
                runId = GameStats.RunId,
                runKills = GameStats.RunKills,
                runDefeats = GameStats.RunDefeats,
                runPlayTime = GameStats.RunPlayTime
            };

            foreach (KeyValuePair<string, int> pair in GameStats.ElementLevels)
            {
                data.elementIds.Add(pair.Key);
                data.elementLevels.Add(pair.Value);
            }
            data.storyFlags.AddRange(GameStats.StoryFlags);
            data.completedQuests.AddRange(GameStats.CompletedQuests);
            return data;
        }

        public static void Restore(RunSaveData data)
        {
            GameStats.ResetAll();

            GameStats.BulletDamage = data.bulletDamage;
            GameStats.FireDelay = data.fireDelay;
            GameStats.ReloadTime = data.reloadTime;
            GameStats.MaxAmmo = data.maxAmmo;
            GameStats.WallMaxHP = data.wallMaxHP;
            GameStats.BulletCount = data.bulletCount;
            GameStats.SpreadAngle = data.spreadAngle;
            GameStats.TowerCount = data.towerCount;
            GameStats.Pierce = data.pierce;
            GameStats.ExplosionLevel = data.explosionLevel;
            GameStats.TowerPowerLevel = data.towerPowerLevel;
            GameStats.Level = Mathf.Max(1, data.level);
            GameStats.Experience = data.experience;
            GameStats.WeaponSkinId = string.IsNullOrEmpty(data.weaponSkinId) ? null : data.weaponSkinId;

            GameStats.Mission = Mathf.Max(1, data.mission);
            GameStats.BossesDefeated = data.bossesDefeated;
            GameStats.CampaignCompleted = data.campaignCompleted;
            GameStats.CurrentTownId = string.IsNullOrEmpty(data.currentTownId) ? null : data.currentTownId;
            // Пустая карта в JSON — карты не было (создастся новая)
            GameStats.Map = data.map != null && data.map.rows != null && data.map.rows.Count > 0 ? data.map : null;
            GameStats.Horseshoes = data.horseshoes;
            GameStats.NextBattleWallDamage = data.nextBattleWallDamage;

            for (int i = 0; i < data.elementIds.Count && i < data.elementLevels.Count; i++)
            {
                GameStats.SetElementLevel(data.elementIds[i], data.elementLevels[i]);
            }
            foreach (string flag in data.storyFlags) GameStats.SetStoryFlag(flag, true);
            foreach (string quest in data.completedQuests) GameStats.CompleteQuest(quest);

            GameStats.RunId = data.runId;
            GameStats.InBattle = false;
            GameStats.RunKills = data.runKills;
            GameStats.RunDefeats = data.runDefeats;
            GameStats.RunPlayTime = data.runPlayTime;
        }

        // ---------------------------------------------------------------- Файлы

        // Сначала во временный файл, потом подмена: обрыв на середине записи не портит сохранение
        public static void WriteAtomic(string path, string content)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temp = path + ".tmp";
            File.WriteAllText(temp, content);
            if (File.Exists(path)) File.Delete(path);
            File.Move(temp, path);
        }
    }
}
