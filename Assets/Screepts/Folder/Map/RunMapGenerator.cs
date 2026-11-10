using System.Collections.Generic;
using UnityEngine;

namespace Tanks2D
{
    // Генерация карты забега:
    //   ряд 0 — стартовый город, ряды 1..N-2 — по 2–3 города, последний ряд — финальная цитадель;
    //   дороги только в соседний ряд и без пересечений; у каждого города есть вход и выход;
    //   города берутся из пула (TownCatalog, 30 штук) без повторов;
    //   сюжетные города (TownDefinition.fixedMapRow) всегда стоят на своём ряду — на них держатся задания.
    public static class RunMapGenerator
    {
        public const int DefaultRows = 10;

        public static RunMap Generate(int seed, IReadOnlyList<TownDefinition> pool, int rowCount = DefaultRows)
        {
            var random = new System.Random(seed);
            var map = new RunMap { seed = seed };

            // Ряды и количество городов
            for (int r = 0; r < rowCount; r++)
            {
                int count = r == 0 || r == rowCount - 1 ? 1 : random.Next(2, 4);
                var row = new MapRow();
                for (int i = 0; i < count; i++) row.nodes.Add(new MapNode { row = r, index = i });
                map.rows.Add(row);
            }

            // Дороги: каждый город ведёт в «свой» город следующего ряда (по относительной позиции)
            // и иногда ещё в соседний — так дороги не пересекаются
            for (int r = 0; r < rowCount - 1; r++)
            {
                List<MapNode> from = map.rows[r].nodes;
                List<MapNode> to = map.rows[r + 1].nodes;

                for (int i = 0; i < from.Count; i++)
                {
                    int lo = Projection(i, from.Count, to.Count, floor: true);
                    int hi = Projection(i, from.Count, to.Count, floor: false);
                    AddEdge(from[i], lo);
                    if (hi != lo && random.NextDouble() < 0.6) AddEdge(from[i], hi);
                }

                // У стартового города — дороги во все города первого ряда (настоящий выбор с самого начала)
                if (r == 0) foreach (MapNode node in to) AddEdge(from[0], node.index);

                // Каждый город следующего ряда должен быть достижим
                for (int j = 0; j < to.Count; j++)
                {
                    bool reached = false;
                    foreach (MapNode node in from) reached |= node.next.Contains(j);
                    if (!reached) AddEdge(from[NearestSource(j, from.Count, to.Count)], j);
                }

                foreach (MapNode node in from) node.next.Sort();
            }

            AssignTowns(map, pool, random);

            map.currentRow = 0;
            map.currentIndex = 0;
            map.visited.Add(new Vector2Int(0, 0));
            return map;
        }

        // Позиция города i из a городов, спроецированная на ряд из b городов
        private static int Projection(int i, int a, int b, bool floor)
        {
            if (a <= 1 || b <= 1) return floor ? 0 : b - 1;
            float t = i * (b - 1) / (float)(a - 1);
            return floor ? Mathf.FloorToInt(t) : Mathf.CeilToInt(t);
        }

        private static int NearestSource(int j, int a, int b)
        {
            if (a <= 1 || b <= 1) return 0;
            return Mathf.Clamp(Mathf.RoundToInt(j * (a - 1) / (float)(b - 1)), 0, a - 1);
        }

        private static void AddEdge(MapNode node, int target)
        {
            if (!node.next.Contains(target)) node.next.Add(target);
        }

        // Города из пула без повторов (в пуле 30, на карте не больше 26).
        // Сюжетные города (TownDefinition.fixedMapRow) всегда встают на свой ряд, остальные — случайно.
        private static void AssignTowns(RunMap map, IReadOnlyList<TownDefinition> pool, System.Random random)
        {
            var towns = new List<TownDefinition>();
            if (pool != null) foreach (TownDefinition town in pool) if (town != null) towns.Add(town);

            for (int i = towns.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (towns[i], towns[j]) = (towns[j], towns[i]);
            }

            // Сначала сюжетные города — в случайный свободный город своего ряда
            var free = new List<TownDefinition>();
            foreach (TownDefinition town in towns)
            {
                MapNode node = town.fixedMapRow >= 0 ? RandomFreeNode(map, town.fixedMapRow, random) : null;
                if (node != null) node.townId = town.Id;
                else free.Add(town);
            }

            // Остальные — по порядку из перемешанного пула (если пул мал, города повторяются)
            List<TownDefinition> rest = free.Count > 0 ? free : towns;
            int next = 0;
            foreach (MapRow row in map.rows)
            {
                foreach (MapNode node in row.nodes)
                {
                    if (node.townId != null) continue;
                    node.townId = rest.Count > 0 ? rest[next % rest.Count].Id : null;
                    next++;
                }
            }
        }

        private static MapNode RandomFreeNode(RunMap map, int row, System.Random random)
        {
            if (row >= map.rows.Count) return null;

            var candidates = new List<MapNode>();
            foreach (MapNode node in map.rows[row].nodes) if (node.townId == null) candidates.Add(node);
            return candidates.Count > 0 ? candidates[random.Next(candidates.Count)] : null;
        }
    }
}
