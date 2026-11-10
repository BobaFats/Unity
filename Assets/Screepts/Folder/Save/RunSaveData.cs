using System;
using System.Collections.Generic;

namespace Tanks2D
{
    // Снимок забега на диске (JSON). Пишется при входе в лобби, деревню и на карту похода.
    // Новое поле прогресса = новое поле здесь + строчка в SaveSystem.Capture / Restore.
    [Serializable]
    public class RunSaveData
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public string scene;
        public string savedAt;

        // Герой и стена
        public int bulletDamage;
        public float fireDelay;
        public float reloadTime;
        public int maxAmmo;
        public int wallMaxHP;
        public int bulletCount;
        public float spreadAngle;
        public int towerCount;
        public int pierce;
        public int explosionLevel;
        public int towerPowerLevel;
        public int level;
        public int experience;
        public List<string> elementIds = new List<string>();
        public List<int> elementLevels = new List<int>();
        public string weaponSkinId;

        // Кампания и поход
        public int mission;
        public int bossesDefeated;
        public bool campaignCompleted;
        public string currentTownId;
        public RunMap map;
        public int horseshoes;
        public float nextBattleWallDamage;

        // Сюжет
        public List<string> storyFlags = new List<string>();
        public List<string> completedQuests = new List<string>();

        // Статистика забега
        public int runId;
        public int runKills;
        public int runDefeats;
        public float runPlayTime;
    }
}
