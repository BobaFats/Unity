using System;
using System.Collections.Generic;
using UnityEngine;

namespace Tanks2D
{
    // Что меняет исход встречи
    public enum EncounterEffectType
    {
        Experience,           // + опыт героя
        BulletDamagePercent,  // ± урон пули в процентах
        AddTower,             // + башня-помощник (до максимума)
        Wheels,               // ± колёса дилижанса (только в поездке)
        WallMaxHP,            // ± максимальная прочность стены
        WallDamageNextBattle, // стена начнёт следующий бой повреждённой (проценты)
        ElementLevel,         // + уровень случайной стихии
        Horseshoes            // ± подковы
    }

    [Serializable]
    public class EncounterEffect
    {
        public EncounterEffectType type;
        public int amount;
    }

    // Карты удачи, как в Hand of Fate: перемешиваются рубашкой вверх, игрок тянет одну
    public enum ChanceResult { HugeSuccess, Success, Failure, HugeFailure }

    [Serializable]
    public class ChanceCard
    {
        public ChanceResult result = ChanceResult.Success;
        [Tooltip("Сколько таких карт в колоде удачи")]
        [Min(0)] public int count = 1;
        [TextArea] public string text;
        public List<EncounterEffect> effects = new List<EncounterEffect>();
    }

    [Serializable]
    public class EncounterChoice
    {
        public string label = "Вариант";
        [TextArea] public string resultText;
        [Tooltip("Прямой исход (если не риск)")]
        public List<EncounterEffect> effects = new List<EncounterEffect>();

        [Tooltip("Риск: исход решают карты удачи")]
        public bool isGamble;
        public List<ChanceCard> chanceCards = new List<ChanceCard>();

        [Tooltip("Цена выбора в подковах (вариант недоступен, если не хватает)")]
        [Min(0)] public int horseshoeCost;
    }

    // Карта встречи в дороге. Создать: Create > Shooter > Encounter, затем добавить в Assets/Resources/EncounterCatalog.
    // Позже встречи будут начинать цепочки квестов (как в Hand of Fate).
    [CreateAssetMenu(fileName = "Encounter", menuName = "Shooter/Encounter")]
    public class EncounterDefinition : ScriptableObject
    {
        [SerializeField] private string _id;
        public string title = "Встреча";
        [TextArea(3, 8)] public string text;
        [Tooltip("Картинка карты. Пусто — заглушка")]
        public Sprite art;
        public Color placeholderColor = new Color(0.55f, 0.45f, 0.3f);
        public List<EncounterChoice> choices = new List<EncounterChoice>();

        public string Id => string.IsNullOrEmpty(_id) ? name : _id;
    }
}
