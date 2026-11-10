using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Tanks2D
{
    // Окно «Наследие»: слава и улучшения, которые действуют на каждый новый забег.
    // Строки строятся из MetaUpgradeCatalog; нажатие на строку — покупка следующего уровня.
    public class LegacyPanel : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private TextMeshProUGUI _gloryText;
        [SerializeField] private Transform _container;
        [Tooltip("Шаблон строки: Button + дочерний TextMeshProUGUI")]
        [SerializeField] private Button _rowTemplate;
        [SerializeField] private Button _closeButton;
        [Tooltip("Кнопка, открывающая окно (необязательно)")]
        [SerializeField] private Button _openButton;

        private readonly List<Button> _rows = new List<Button>();

        public bool IsOpen => _root != null && _root.activeSelf;
        public IReadOnlyList<Button> Rows => _rows;

        private void Awake()
        {
            if (_rowTemplate != null) _rowTemplate.gameObject.SetActive(false);
            if (_closeButton != null) _closeButton.onClick.AddListener(Close);
            if (_openButton != null) _openButton.onClick.AddListener(Open);
            if (_root != null) _root.SetActive(false);
        }

        public void Open()
        {
            if (_root == null) return;
            _root.SetActive(true);
            Rebuild();
        }

        public void Close()
        {
            if (_root != null) _root.SetActive(false);
        }

        public bool Buy(int index)
        {
            MetaUpgradeCatalog catalog = MetaUpgradeCatalog.Instance;
            if (catalog == null || index < 0 || index >= catalog.Upgrades.Count) return false;

            bool bought = MetaProgress.Buy(catalog.Upgrades[index]);
            Rebuild();
            return bought;
        }

        private void Rebuild()
        {
            foreach (Button row in _rows)
            {
                row.gameObject.SetActive(false);
                Destroy(row.gameObject);
            }
            _rows.Clear();

            if (_gloryText != null) _gloryText.text = $"Слава: <b>{MetaProgress.Glory}</b>";

            MetaUpgradeCatalog catalog = MetaUpgradeCatalog.Instance;
            if (catalog == null || _rowTemplate == null) return;

            for (int i = 0; i < catalog.Upgrades.Count; i++)
            {
                MetaUpgrade upgrade = catalog.Upgrades[i];
                if (upgrade == null) continue;

                int level = MetaProgress.LevelOf(upgrade.id);
                bool maxed = level >= upgrade.maxLevel;

                Button row = Instantiate(_rowTemplate, _container != null ? _container : _rowTemplate.transform.parent);
                row.name = $"Upgrade_{upgrade.id}";
                row.gameObject.SetActive(true);
                row.interactable = MetaProgress.CanBuy(upgrade);

                string now = level > 0 ? upgrade.Describe(level) : "нет";
                string price = maxed ? "<color=#5BE06A>максимум</color>" : $"следующий: {upgrade.Describe(level + 1)} за <b>{upgrade.CostOf(level)}</b> славы";
                TextMeshProUGUI label = row.GetComponentInChildren<TextMeshProUGUI>(true);
                if (label != null) label.text = $"<b>{upgrade.title}</b>  {level}/{upgrade.maxLevel}\n<size=75%>сейчас: {now}; {price}</size>";

                int captured = i;
                row.onClick.AddListener(() => Buy(captured));
                _rows.Add(row);
            }
        }
    }
}
