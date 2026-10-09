using System.Collections.Generic;
using UnityEngine;

namespace Tanks2D
{
    // Все скины оружия героя (Assets/Resources/WeaponSkinCatalog.asset). Первый — скин по умолчанию.
    [CreateAssetMenu(fileName = "WeaponSkinCatalog", menuName = "Shooter/Weapon Skin Catalog")]
    public class WeaponSkinCatalog : ScriptableObject
    {
        public const string ResourcePath = "WeaponSkinCatalog";

        [SerializeField] private List<WeaponSkin> _skins = new List<WeaponSkin>();

        private static WeaponSkinCatalog _instance;

        public static WeaponSkinCatalog Instance
        {
            get
            {
                if (_instance == null) _instance = Resources.Load<WeaponSkinCatalog>(ResourcePath);
                return _instance;
            }
        }

        public static void SetInstance(WeaponSkinCatalog catalog) => _instance = catalog;

        public IReadOnlyList<WeaponSkin> Skins => _skins;

        // Выбранный игроком скин (GameStats.WeaponSkinId); не найден — первый
        public static WeaponSkin Selected
        {
            get
            {
                WeaponSkinCatalog catalog = Instance;
                if (catalog == null || catalog._skins.Count == 0) return null;
                int index = catalog.IndexOf(GameStats.WeaponSkinId);
                return catalog._skins[Mathf.Max(0, index)];
            }
        }

        public int IndexOf(string id)
        {
            for (int i = 0; i < _skins.Count; i++)
            {
                if (_skins[i] != null && _skins[i].Id == id) return i;
            }
            return -1;
        }

        // Переключить выбранный скин на step позиций (по кругу)
        public static WeaponSkin Cycle(int step)
        {
            WeaponSkinCatalog catalog = Instance;
            if (catalog == null || catalog._skins.Count == 0) return null;

            int index = Mathf.Max(0, catalog.IndexOf(GameStats.WeaponSkinId));
            index = (index + step % catalog._skins.Count + catalog._skins.Count) % catalog._skins.Count;
            WeaponSkin skin = catalog._skins[index];
            GameStats.WeaponSkinId = skin != null ? skin.Id : null;
            return skin;
        }

        public void Add(WeaponSkin skin)
        {
            if (skin != null && !_skins.Contains(skin)) _skins.Add(skin);
        }
    }
}
