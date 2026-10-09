using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Tanks2D
{
    // Выбор скина оружия в лобби: стрелки ◀ ▶, название и картинка
    public class WeaponSkinSelector : MonoBehaviour
    {
        [SerializeField] private Button _previous;
        [SerializeField] private Button _next;
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private Image _icon;

        private void Start()
        {
            if (_previous != null) _previous.onClick.AddListener(() => Select(-1));
            if (_next != null) _next.onClick.AddListener(() => Select(1));
            Refresh();
        }

        public void Select(int step)
        {
            WeaponSkinCatalog.Cycle(step);
            Refresh();
        }

        private void Refresh()
        {
            WeaponSkin skin = WeaponSkinCatalog.Selected;
            WeaponSkinCatalog catalog = WeaponSkinCatalog.Instance;
            int count = catalog != null ? catalog.Skins.Count : 0;
            int index = skin != null && catalog != null ? catalog.IndexOf(skin.Id) + 1 : 0;

            if (_nameText != null) _nameText.text = skin != null ? $"Оружие: {skin.displayName}  <size=70%>({index}/{count})</size>" : "Оружие: стандарт";

            if (_icon != null)
            {
                Sprite preview = skin != null ? skin.PreviewSprite : null;
                VisualEntry weapon = VisualCatalog.Resolve(VisualId.Weapon);

                _icon.sprite = preview != null ? preview : PlaceholderSprites.Get(weapon.shape);
                _icon.color = preview != null ? Color.white : (skin != null ? skin.tint * weapon.color : weapon.color);
                _icon.preserveAspect = true;
            }
        }
    }
}
