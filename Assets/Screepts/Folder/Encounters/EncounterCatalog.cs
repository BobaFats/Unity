using System.Collections.Generic;
using UnityEngine;

namespace Tanks2D
{
    // Колода встреч в дороге (Assets/Resources/EncounterCatalog.asset)
    [CreateAssetMenu(fileName = "EncounterCatalog", menuName = "Shooter/Encounter Catalog")]
    public class EncounterCatalog : ScriptableObject
    {
        public const string ResourcePath = "EncounterCatalog";

        [SerializeField] private List<EncounterDefinition> _encounters = new List<EncounterDefinition>();

        private static EncounterCatalog _instance;

        public static EncounterCatalog Instance
        {
            get
            {
                if (_instance == null) _instance = Resources.Load<EncounterCatalog>(ResourcePath);
                return _instance;
            }
        }

        public static void SetInstance(EncounterCatalog catalog) => _instance = catalog;

        public IReadOnlyList<EncounterDefinition> Encounters => _encounters;

        // count разных встреч в случайном порядке
        public List<EncounterDefinition> Draw(int count)
        {
            var deck = new List<EncounterDefinition>();
            foreach (EncounterDefinition encounter in _encounters) if (encounter != null) deck.Add(encounter);

            for (int i = deck.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (deck[i], deck[j]) = (deck[j], deck[i]);
            }

            if (deck.Count > count) deck.RemoveRange(count, deck.Count - count);
            return deck;
        }

        public void Add(EncounterDefinition encounter)
        {
            if (encounter != null && !_encounters.Contains(encounter)) _encounters.Add(encounter);
        }
    }
}
