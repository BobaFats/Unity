using System.Collections.Generic;
using UnityEngine;

namespace Tanks2D
{
    // Белые фигуры с тёмной обводкой; цвет задаётся через SpriteRenderer.color / Image.color.
    // Редакторный сборщик сохраняет их в Assets/Resources/Placeholders/, иначе генерируются на лету.
    public static class PlaceholderSprites
    {
        public const int TextureSize = 128;
        public const string ResourceFolder = "Placeholders";

        private const float OutlineWidth = 0.1f;
        private const float OutlineShade = 0.45f;

        private static readonly Dictionary<PlaceholderShape, Sprite> _cache = new Dictionary<PlaceholderShape, Sprite>();

        public static Sprite Get(PlaceholderShape shape)
        {
            if (_cache.TryGetValue(shape, out Sprite cached) && cached != null) return cached;

            Sprite sprite = Resources.Load<Sprite>($"{ResourceFolder}/{shape}");
            if (sprite == null)
            {
                Texture2D texture = CreateTexture(shape);
                sprite = Sprite.Create(texture, new Rect(0, 0, TextureSize, TextureSize), new Vector2(0.5f, 0.5f), TextureSize);
                sprite.name = $"Placeholder_{shape}";
            }

            _cache[shape] = sprite;
            return sprite;
        }

        // Сбросить кэш после (пере)импорта PNG-заглушек, чтобы брались спрайты-ассеты
        public static void ClearCache()
        {
            _cache.Clear();
        }

        public static Texture2D CreateTexture(PlaceholderShape shape)
        {
            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
            {
                name = $"Placeholder_{shape}",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color32[TextureSize * TextureSize];
            float pixelSize = 2f / TextureSize;

            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    // Координаты центра пикселя в диапазоне [-1, 1]
                    var p = new Vector2((x + 0.5f) * pixelSize - 1f, (y + 0.5f) * pixelSize - 1f);
                    float distance = SignedDistance(shape, p);

                    float alpha = Mathf.Clamp01(0.5f - distance / pixelSize);
                    float outline = Mathf.Clamp01(0.5f - (distance + OutlineWidth) / pixelSize);
                    float shade = Mathf.Lerp(OutlineShade, 1f, outline);

                    byte c = (byte)(shade * 255f);
                    pixels[y * TextureSize + x] = new Color32(c, c, c, (byte)(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        // Отрицательное значение — внутри фигуры
        private static float SignedDistance(PlaceholderShape shape, Vector2 p)
        {
            const float r = 0.97f;

            switch (shape)
            {
                case PlaceholderShape.Circle:
                    return p.magnitude - r;

                case PlaceholderShape.Triangle:
                    return ConvexPolygonDistance(p, new Vector2(-r, -r * 0.9f), new Vector2(r, -r * 0.9f), new Vector2(0f, r));

                case PlaceholderShape.Diamond:
                    return ConvexPolygonDistance(p, new Vector2(0f, -r), new Vector2(r, 0f), new Vector2(0f, r), new Vector2(-r, 0f));

                default:
                    return Mathf.Max(Mathf.Abs(p.x), Mathf.Abs(p.y)) - r;
            }
        }

        // Вершины против часовой стрелки
        private static float ConvexPolygonDistance(Vector2 p, params Vector2[] vertices)
        {
            float distance = float.MinValue;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector2 a = vertices[i];
                Vector2 b = vertices[(i + 1) % vertices.Length];
                Vector2 edge = (b - a).normalized;
                var outward = new Vector2(edge.y, -edge.x);
                distance = Mathf.Max(distance, Vector2.Dot(p - a, outward));
            }
            return distance;
        }
    }
}
