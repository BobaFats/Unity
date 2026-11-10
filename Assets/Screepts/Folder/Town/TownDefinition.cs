using System;
using System.Collections.Generic;
using UnityEngine;

namespace Tanks2D
{
    // Житель здания — квестгивер.
    // Задания к жителю привязываются со стороны задания: в ассете QuestDefinition указываются
    // город, id здания и id жителя (поле «giverNpcId»).
    [Serializable]
    public class NpcDefinition
    {
        public string id = "npc";
        public string displayName = "Житель";
        [Tooltip("Портрет. Пусто — заглушка")]
        public Sprite portrait;
        public Color placeholderColor = new Color(0.8f, 0.7f, 0.55f);

        [Tooltip("Разговор с героем, когда у жителя нет задания. Пусто — стандартный (УУУУУ / ААААА / Спасибо)")]
        public List<DialogueLine> dialogue = new List<DialogueLine>();
    }

    // Здание на панораме города
    [Serializable]
    public class BuildingDefinition
    {
        public string id = "building";
        public string displayName = "Здание";
        [TextArea] public string description;

        [Header("Вид")]
        [Tooltip("Картинка здания. Пусто — заглушка")]
        public Sprite sprite;
        public PlaceholderShape placeholderShape = PlaceholderShape.Rectangle;
        public Color placeholderColor = new Color(0.7f, 0.6f, 0.45f);

        [Header("Место на панораме (доли 0..1, отсчёт от левого верхнего угла)")]
        public Rect area = new Rect(0.4f, 0.4f, 0.15f, 0.25f);

        [Header("Жители")]
        public List<NpcDefinition> npcs = new List<NpcDefinition>();
    }

    // Город (локация). Assets/Resources/Towns/Town_XX.asset
    [CreateAssetMenu(fileName = "Town", menuName = "Shooter/Town")]
    public class TownDefinition : ScriptableObject
    {
        [Tooltip("Уникальный ключ города. Пусто — имя ассета")]
        [SerializeField] private string _id;
        public string displayName = "Город";
        [TextArea] public string description;

        [Header("Панорама")]
        [Tooltip("Фон города. Пусто — заглушка (небо и земля)")]
        public Sprite background;
        public Color skyColor = new Color(0.45f, 0.65f, 0.9f);
        public Color groundColor = new Color(0.35f, 0.55f, 0.3f);
        [Tooltip("Соотношение сторон панорамы (ширина / высота). У городов Героев 3 — около 2.14")]
        [Min(0.5f)] public float panoramaAspect = 2.14f;

        [Header("Сюжет")]
        [Tooltip("Уровни кампании, которые проходятся из этого города (включительно)")]
        [Min(1)] public int firstLevel = 1;
        [Min(1)] public int lastLevel = 3;
        [Tooltip("Не используется: дороги между городами строит карта забега (RunMapGenerator)")]
        public List<TownDefinition> roadsTo = new List<TownDefinition>();
        [Tooltip("Сюжетный город: всегда стоит на этом ряду карты забега (0 — стартовый город, 1 — следующий ряд...). -1 — попадает на карту случайно")]
        [Min(-1)] public int fixedMapRow = -1;

        [Header("Здания")]
        public List<BuildingDefinition> buildings = new List<BuildingDefinition>();

        public string Id => string.IsNullOrEmpty(_id) ? name : _id;
        public bool ContainsLevel(int level) => level >= firstLevel && level <= lastLevel;
    }
}
