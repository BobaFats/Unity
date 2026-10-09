using UnityEngine;

namespace Tanks2D
{
    // Скин оружия героя. Создать: Create > Shooter > Weapon Skin, затем добавить в Assets/Resources/WeaponSkinCatalog.asset.
    // Пустой скин (без спрайта и модели) = оружие как настроено в сцене.
    [CreateAssetMenu(fileName = "WeaponSkin", menuName = "Shooter/Weapon Skin")]
    public class WeaponSkin : ScriptableObject
    {
        [Tooltip("Уникальный ключ. Пусто — имя ассета")]
        [SerializeField] private string _id;
        public string displayName = "Скин";
        [Tooltip("Картинка для выбора в лобби. Пусто — берётся спрайт скина")]
        public Sprite icon;

        [Header("Вид оружия")]
        public Sprite sprite;
        [Tooltip("Модель оружия (префаб). Важнее спрайта")]
        public GameObject prefab;
        public Color tint = Color.white;

        [Header("Положение (необязательно)")]
        public bool overridePlacement;
        public Vector3 offset;
        public Vector3 rotation;
        [Min(0.01f)] public float scale = 1f;

        [Header("Дуло — откуда вылетают пули (необязательно)")]
        public bool overrideFirePoint;
        [Tooltip("Позиция дула относительно поворотной части (Turret)")]
        public Vector3 firePointLocalPosition = new Vector3(0f, 1.2f, 0f);

        public string Id => string.IsNullOrEmpty(_id) ? name : _id;
        public bool IsDefault => sprite == null && prefab == null && !overridePlacement && !overrideFirePoint && tint == Color.white;
        public Sprite PreviewSprite => icon != null ? icon : sprite;
    }
}
