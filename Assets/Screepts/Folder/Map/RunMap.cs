using System;
using System.Collections.Generic;
using UnityEngine;

namespace Tanks2D
{
    // Город на карте забега
    [Serializable]
    public class MapNode
    {
        public int row;
        public int index;
        public string townId;
        [Tooltip("Индексы городов следующего ряда, куда ведут дороги")]
        public List<int> next = new List<int>();
    }

    [Serializable]
    public class MapRow
    {
        public List<MapNode> nodes = new List<MapNode>();
    }

    // Карта одного забега (по мотивам Slay the Spire): ряды городов снизу вверх, дороги только в соседний ряд.
    // Каждый город даёт 3 уровня, поэтому ряд r — это уровни 3r+1 … 3r+3.
    [Serializable]
    public class RunMap
    {
        public const int LevelsPerTown = 3;

        public int seed;
        public List<MapRow> rows = new List<MapRow>();
        public int currentRow;
        public int currentIndex;
        [Tooltip("Намеченный игроком город следующего ряда (-1 — не выбран)")]
        public int plannedIndex = -1;
        public List<Vector2Int> visited = new List<Vector2Int>();

        public int RowCount => rows.Count;
        public MapNode Current => Get(currentRow, currentIndex);
        public bool IsFinalRow => currentRow >= rows.Count - 1;

        public MapNode Get(int row, int index)
        {
            if (row < 0 || row >= rows.Count) return null;
            List<MapNode> nodes = rows[row].nodes;
            return index >= 0 && index < nodes.Count ? nodes[index] : null;
        }

        // Города, куда можно поехать из текущего
        public List<MapNode> NextOptions
        {
            get
            {
                var options = new List<MapNode>();
                MapNode current = Current;
                if (current == null || IsFinalRow) return options;
                foreach (int i in current.next)
                {
                    MapNode node = Get(currentRow + 1, i);
                    if (node != null) options.Add(node);
                }
                return options;
            }
        }

        public bool IsVisited(int row, int index) => visited.Contains(new Vector2Int(row, index));

        // Переезд в город следующего ряда
        public bool TravelTo(int index)
        {
            MapNode current = Current;
            if (current == null || IsFinalRow || !current.next.Contains(index)) return false;

            currentRow++;
            currentIndex = index;
            plannedIndex = -1;
            visited.Add(new Vector2Int(currentRow, currentIndex));
            return true;
        }

        public static int FirstLevelOfRow(int row) => row * LevelsPerTown + 1;
        public static int LastLevelOfRow(int row) => row * LevelsPerTown + LevelsPerTown;
    }
}
