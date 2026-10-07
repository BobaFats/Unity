using System.Collections.Generic;
using UnityEngine;

namespace Tanks2D
{
    // Список всех стихий игры (Assets/Resources/ElementCatalog.asset)
    [CreateAssetMenu(fileName = "ElementCatalog", menuName = "Shooter/Element Catalog")]
    public class ElementCatalog : ScriptableObject
    {
        public const string ResourcePath = "ElementCatalog";

        [SerializeField] private List<ElementDefinition> _elements = new List<ElementDefinition>();

        private static ElementCatalog _instance;
        private readonly List<ElementDefinition> _sorted = new List<ElementDefinition>();

        public static ElementCatalog Instance
        {
            get
            {
                if (_instance == null) _instance = Resources.Load<ElementCatalog>(ResourcePath);
                return _instance;
            }
        }

        public static void SetInstance(ElementCatalog catalog)
        {
            _instance = catalog;
        }

        public IReadOnlyList<ElementDefinition> Elements => _elements;

        // Стихии в порядке обработки (ElementDefinition.Order)
        public IReadOnlyList<ElementDefinition> Sorted
        {
            get
            {
                if (_sorted.Count != _elements.Count)
                {
                    _sorted.Clear();
                    foreach (ElementDefinition element in _elements)
                    {
                        if (element != null) _sorted.Add(element);
                    }
                    _sorted.Sort((a, b) => a.Order.CompareTo(b.Order));
                }
                return _sorted;
            }
        }

        public bool Contains(ElementDefinition element) => _elements.Contains(element);

        public void Add(ElementDefinition element)
        {
            if (element == null || _elements.Contains(element)) return;
            _elements.Add(element);
            _sorted.Clear();
        }

        private void OnValidate()
        {
            _sorted.Clear();
        }
    }
}
