using System.Collections.Generic;
using UnityEngine;

namespace FrogCart.Runtime
{
    /// <summary>
    /// Процедурная генерация спрайтов: скруглённые прямоугольники с независимым радиусом
    /// на каждый угол, линейные градиенты под углом, inset-блики и фаски, обводки,
    /// круги с радиальной заливкой и мягкие эллиптические тени.
    ///
    /// Зачем: docs/unity-spec/02-art.md описывает всю графику как CSS-примитивы. Нарезать
    /// их руками в редакторе невозможно воспроизводимо, а генерация из тех же чисел даёт
    /// ровно то, что написано в спеке, и правится вместе с ней.
    ///
    /// Сглаживание — суперсэмплинг 4x4. Спрайты кешируются по ключу: одинаковых блоков
    /// на экране до 224 штук, а текстура под ними должна быть одна.
    /// </summary>
    public static class ProcSprite
    {
        const int SS = 4;                 // суперсэмплинг по стороне
        const float PixelsPerUnit = 100f;

        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        // ── публичное API ────────────────────────────────────────────────────────────

        public struct Rounded
        {
            public int w, h;
            public Vector4 radii;          // TL, TR, BR, BL
            public float gradientAngleDeg; // 0° — слева направо, 90° — сверху вниз
            public Color c0, c1, c2;       // три остановки
            public float midStop;          // положение средней остановки, 0..1
            public float insetTop;         // высота верхнего блика в пикселях
            public Color insetTopColor;
            public float insetBottom;      // высота нижней фаски
            public Color insetBottomColor;
            public float outline;
            public Color outlineColor;
            public string key;             // ключ кеша; пустой — не кешировать

            public static Rounded Flat(int w, int h, float radius, Color color, string key = null)
                => new Rounded
                {
                    w = w, h = h,
                    radii = new Vector4(radius, radius, radius, radius),
                    gradientAngleDeg = 90f,
                    c0 = color, c1 = color, c2 = color, midStop = 0.5f,
                    insetTopColor = Color.clear, insetBottomColor = Color.clear,
                    outlineColor = Color.clear,
                    key = key,
                };
        }

        public static Sprite Make(Rounded s)
        {
            if (!string.IsNullOrEmpty(s.key) && Cache.TryGetValue(s.key, out var cached))
                return cached;

            var tex = new Texture2D(s.w, s.h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            var pixels = new Color[s.w * s.h];

            for (int y = 0; y < s.h; y++)
            for (int x = 0; x < s.w; x++)
                pixels[y * s.w + x] = SamplePixel(s, x, y);

            tex.SetPixels(pixels);
            tex.Apply(false, false);

            var sprite = Sprite.Create(tex, new Rect(0, 0, s.w, s.h),
                                       new Vector2(0.5f, 0.5f), PixelsPerUnit);

            if (!string.IsNullOrEmpty(s.key)) Cache[s.key] = sprite;
            return sprite;
        }

        /// <summary>Круг с радиальной заливкой от центра к краю.</summary>
        public static Sprite Circle(int diameter, Color inner, Color outer,
                                    float outlineWidth, Color outlineColor, string key = null)
        {
            if (!string.IsNullOrEmpty(key) && Cache.TryGetValue(key, out var cached) && cached != null)
                return cached;

            var tex = new Texture2D(diameter, diameter, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            float r = diameter * 0.5f;
            var pixels = new Color[diameter * diameter];

            for (int y = 0; y < diameter; y++)
            for (int x = 0; x < diameter; x++)
            {
                float alpha = 0f;
                Color acc = Color.clear;

                for (int sy = 0; sy < SS; sy++)
                for (int sx = 0; sx < SS; sx++)
                {
                    float px = x + (sx + 0.5f) / SS;
                    float py = y + (sy + 0.5f) / SS;
                    float d = Vector2.Distance(new Vector2(px, py), new Vector2(r, r));

                    if (d > r) continue;

                    Color c = Color.Lerp(inner, outer, Mathf.Clamp01(d / r));
                    if (outlineWidth > 0f && d > r - outlineWidth) c = outlineColor;

                    acc += c;
                    alpha += 1f;
                }

                pixels[y * diameter + x] = alpha <= 0f
                    ? Color.clear
                    : Premul(acc / alpha, alpha / (SS * SS));
            }

            tex.SetPixels(pixels);
            tex.Apply(false, false);

            var sprite = Sprite.Create(tex, new Rect(0, 0, diameter, diameter),
                                       new Vector2(0.5f, 0.5f), PixelsPerUnit);

            if (!string.IsNullOrEmpty(key)) Cache[key] = sprite;
            return sprite;
        }

        /// <summary>
        /// Кольцо по контуру скруглённого прямоугольника — слой рельсов.
        /// w, h — размеры осевой линии; текстура выходит на stroke/2 в обе стороны.
        /// dash &gt; 0 включает пунктир: dash пикселей штрих, gap пикселей пропуск.
        /// </summary>
        public static Sprite Ring(int w, int h, float radius, float stroke, Color color,
                                  float dash = 0f, float gap = 0f, string key = null)
        {
            if (!string.IsNullOrEmpty(key) && Cache.TryGetValue(key, out var cached) && cached != null)
                return cached;

            int pad = Mathf.CeilToInt(stroke * 0.5f) + 1;
            int tw = w + pad * 2;
            int th = h + pad * 2;

            var tex = new Texture2D(tw, th, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            var pixels = new Color[tw * th];
            float half = stroke * 0.5f;

            for (int y = 0; y < th; y++)
            for (int x = 0; x < tw; x++)
            {
                int hits = 0;

                for (int sy = 0; sy < SS; sy++)
                for (int sx = 0; sx < SS; sx++)
                {
                    float px = x + (sx + 0.5f) / SS - pad;
                    float py = y + (sy + 0.5f) / SS - pad;

                    float d = Mathf.Abs(RoundedRectDistance(px, py, w, h, radius));
                    if (d > half) continue;

                    if (dash > 0f)
                    {
                        float along = ApproxArcLength(px, py, w, h);
                        if (Mathf.Repeat(along, dash + gap) > dash) continue;
                    }

                    hits++;
                }

                pixels[y * tw + x] = hits == 0
                    ? Color.clear
                    : new Color(color.r, color.g, color.b, color.a * hits / (SS * SS));
            }

            tex.SetPixels(pixels);
            tex.Apply(false, false);

            var sprite = Sprite.Create(tex, new Rect(0, 0, tw, th),
                                       new Vector2(0.5f, 0.5f), PixelsPerUnit);

            if (!string.IsNullOrEmpty(key)) Cache[key] = sprite;
            return sprite;
        }

        /// <summary>Знаковое расстояние до контура скруглённого прямоугольника.</summary>
        static float RoundedRectDistance(float px, float py, float w, float h, float r)
        {
            float cx = w * 0.5f;
            float cy = h * 0.5f;

            float qx = Mathf.Abs(px - cx) - (cx - r);
            float qy = Mathf.Abs(py - cy) - (cy - r);

            float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
            float inside = Mathf.Min(Mathf.Max(qx, qy), 0f);

            return outside + inside - r;
        }

        /// <summary>Грубая длина дуги от верхнего левого угла — только для фазы пунктира.</summary>
        static float ApproxArcLength(float px, float py, float w, float h)
            => px + py * 1.61803f;

        /// <summary>Мягкая эллиптическая тень — контактная тень под объектами.</summary>
        public static Sprite SoftEllipse(int w, int h, Color color, float blur, string key = null)
        {
            if (!string.IsNullOrEmpty(key) && Cache.TryGetValue(key, out var cached) && cached != null)
                return cached;

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            float rx = w * 0.5f;
            float ry = h * 0.5f;
            var pixels = new Color[w * h];

            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float nx = (x + 0.5f - rx) / rx;
                float ny = (y + 0.5f - ry) / ry;
                float d = Mathf.Sqrt(nx * nx + ny * ny);

                // Мягкий край шириной blur, выраженной в долях полуоси.
                float edge = blur <= 0f ? 0.02f : Mathf.Clamp01(blur / Mathf.Max(rx, ry));
                float a = 1f - Mathf.SmoothStep(1f - edge, 1f, d);

                pixels[y * w + x] = new Color(color.r, color.g, color.b, color.a * a);
            }

            tex.SetPixels(pixels);
            tex.Apply(false, false);

            var sprite = Sprite.Create(tex, new Rect(0, 0, w, h),
                                       new Vector2(0.5f, 0.5f), PixelsPerUnit);

            if (!string.IsNullOrEmpty(key)) Cache[key] = sprite;
            return sprite;
        }

        /// <summary>
        /// Пятиконечная звезда с мягким вертикальным градиентом и тёмной обводкой.
        /// Спека (02-art.md) рисует звёзды через clip-path; здесь тот же силуэт
        /// считается по лучам, потому что круг вместо звезды выглядел заглушкой.
        /// </summary>
        public static Sprite Star(int size, Color top, Color bottom,
                                  float outlineWidth, Color outlineColor, string key = null)
        {
            if (!string.IsNullOrEmpty(key) && Cache.TryGetValue(key, out var cached) && cached != null)
                return cached;

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            var pixels = new Color[size * size];
            float half = size * 0.5f;
            float outer = half * 0.95f;
            // 0.42 давало тощую «морскую звезду»; 0.52 ближе к плотной звезде из спеки.
            float inner = outer * 0.52f;

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int hits = 0;
                int edgeHits = 0;

                for (int sy = 0; sy < SS; sy++)
                for (int sx = 0; sx < SS; sx++)
                {
                    float px = x + (sx + 0.5f) / SS - half;
                    // Ось Y текстуры смотрит вверх, а звезда должна стоять остриём вверх.
                    float py = half - (y + (sy + 0.5f) / SS);

                    float radius = StarRadius(px, py, outer, inner);
                    float distance = Mathf.Sqrt(px * px + py * py);

                    if (distance > radius) continue;

                    hits++;
                    if (outlineWidth > 0f && distance > radius - outlineWidth) edgeHits++;
                }

                if (hits == 0) { pixels[y * size + x] = Color.clear; continue; }

                float t = y / (float)(size - 1);
                Color fill = Color.Lerp(top, bottom, t);
                if (edgeHits * 2 > hits) fill = outlineColor;

                pixels[y * size + x] = new Color(fill.r, fill.g, fill.b,
                                                 fill.a * hits / (SS * SS));
            }

            tex.SetPixels(pixels);
            tex.Apply(false, false);

            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size),
                                       new Vector2(0.5f, 0.5f), PixelsPerUnit);

            if (!string.IsNullOrEmpty(key)) Cache[key] = sprite;
            return sprite;
        }

        /// <summary>Радиус звезды в направлении точки: пила между лучом и впадиной.</summary>
        static float StarRadius(float px, float py, float outer, float inner)
        {
            const int Points = 5;
            const float Sector = Mathf.PI * 2f / Points;

            float angle = Mathf.Atan2(px, py);              // 0 — вверх, остриё
            float local = Mathf.Repeat(angle, Sector) / Sector;   // 0..1 внутри сектора
            float wave = Mathf.Abs(local - 0.5f) * 2f;            // 1 на луче, 0 во впадине

            return Mathf.Lerp(inner, outer, wave);
        }

        /// <summary>Кольцо с разрывом — иконка «повторить» на кнопке Retry.</summary>
        public static Sprite Arc(int size, float stroke, Color color,
                                 float gapDeg, float rotationDeg, string key = null)
        {
            if (!string.IsNullOrEmpty(key) && Cache.TryGetValue(key, out var cached) && cached != null)
                return cached;

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            float half = size * 0.5f;
            float radius = half - stroke * 0.5f;
            var pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int hits = 0;

                for (int sy = 0; sy < SS; sy++)
                for (int sx = 0; sx < SS; sx++)
                {
                    float px = x + (sx + 0.5f) / SS - half;
                    float py = y + (sy + 0.5f) / SS - half;

                    float distance = Mathf.Sqrt(px * px + py * py);
                    if (Mathf.Abs(distance - radius) > stroke * 0.5f) continue;

                    float angle = Mathf.Repeat(Mathf.Atan2(py, px) * Mathf.Rad2Deg - rotationDeg, 360f);
                    if (angle < gapDeg) continue;

                    hits++;
                }

                pixels[y * size + x] = hits == 0
                    ? Color.clear
                    : new Color(color.r, color.g, color.b, color.a * hits / (SS * SS));
            }

            tex.SetPixels(pixels);
            tex.Apply(false, false);

            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size),
                                       new Vector2(0.5f, 0.5f), PixelsPerUnit);

            if (!string.IsNullOrEmpty(key)) Cache[key] = sprite;
            return sprite;
        }

        /// <summary>Треугольник остриём вправо — иконка «продолжить» на кнопке Resume.</summary>
        public static Sprite Triangle(int width, int height, Color color, string key = null)
        {
            if (!string.IsNullOrEmpty(key) && Cache.TryGetValue(key, out var cached) && cached != null)
                return cached;

            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            var pixels = new Color[width * height];

            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int hits = 0;

                for (int sy = 0; sy < SS; sy++)
                for (int sx = 0; sx < SS; sx++)
                {
                    float px = (x + (sx + 0.5f) / SS) / width;          // 0..1 слева направо
                    float py = (y + (sy + 0.5f) / SS) / height;         // 0..1 снизу вверх

                    // Клин: чем правее, тем уже допустимый разброс по вертикали.
                    float halfSpan = 0.5f * (1f - px);
                    if (Mathf.Abs(py - 0.5f) > halfSpan) continue;

                    hits++;
                }

                pixels[y * width + x] = hits == 0
                    ? Color.clear
                    : new Color(color.r, color.g, color.b, color.a * hits / (SS * SS));
            }

            tex.SetPixels(pixels);
            tex.Apply(false, false);

            var sprite = Sprite.Create(tex, new Rect(0, 0, width, height),
                                       new Vector2(0.5f, 0.5f), PixelsPerUnit);

            if (!string.IsNullOrEmpty(key)) Cache[key] = sprite;
            return sprite;
        }

        /// <summary>Белый пиксель — для сплошных заливок и полос.</summary>
        public static Sprite White()
        {
            if (Cache.TryGetValue("white", out var cached)) return cached;

            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var pixels = new Color[16];
            for (int i = 0; i < 16; i++) pixels[i] = Color.white;
            tex.SetPixels(pixels);
            tex.Apply(false, false);

            var sprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), PixelsPerUnit);
            Cache["white"] = sprite;
            return sprite;
        }

        public static Color Hex(string hex, float alpha = 1f)
        {
            hex = hex.TrimStart('#');
            byte r = System.Convert.ToByte(hex.Substring(0, 2), 16);
            byte g = System.Convert.ToByte(hex.Substring(2, 2), 16);
            byte b = System.Convert.ToByte(hex.Substring(4, 2), 16);
            return new Color32(r, g, b, (byte)Mathf.RoundToInt(alpha * 255f));
        }

        public static void ClearCache() => Cache.Clear();

        /// <summary>
        /// Сброс кеша при старте каждой игровой сессии.
        ///
        /// В билде это лишнее — процесс новый, кеш пуст. Нужно в редакторе:
        /// статические поля переживают выход из Play mode, а сами объекты Unity —
        /// спрайты, материалы, меши, текстуры — при выходе уничтожаются. На втором
        /// запуске из кеша доставались уничтоженные объекты, и всё, что ими
        /// красится, становилось белым: головы жаб, таблички очереди, планка HUD.
        /// В плеере при этом всё было в порядке, и разница «работает в билде,
        /// сломано в редакторе» уводила поиск совсем не туда.
        ///
        /// SubsystemRegistration выполняется до первого Awake, поэтому к моменту
        /// сборки сцены кеш заведомо чист.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => ClearCache();

        // ── внутренности ─────────────────────────────────────────────────────────────

        static Color SamplePixel(in Rounded s, int x, int y)
        {
            Color acc = Color.clear;
            int hits = 0;

            for (int sy = 0; sy < SS; sy++)
            for (int sx = 0; sx < SS; sx++)
            {
                float px = x + (sx + 0.5f) / SS;
                float py = y + (sy + 0.5f) / SS;

                if (!Inside(s, px, py, out float edgeDist)) continue;

                acc += ShadeAt(s, px, py, edgeDist);
                hits++;
            }

            if (hits == 0) return Color.clear;

            float coverage = hits / (float)(SS * SS);
            return Premul(acc / hits, coverage);
        }

        static Color ShadeAt(in Rounded s, float px, float py, float edgeDist)
        {
            Color c = Gradient(s, px, py);

            // Верхний блик и нижняя фаска — полосы по внутреннему краю формы,
            // ровно как CSS inset box-shadow из спеки.
            if (s.insetTop > 0f && py < s.insetTop)
            {
                float t = 1f - py / s.insetTop;
                c = Blend(c, s.insetTopColor, s.insetTopColor.a * t);
            }

            if (s.insetBottom > 0f && py > s.h - s.insetBottom)
            {
                float t = 1f - (s.h - py) / s.insetBottom;
                c = Blend(c, s.insetBottomColor, s.insetBottomColor.a * t);
            }

            if (s.outline > 0f && edgeDist < s.outline)
                c = Blend(c, s.outlineColor, s.outlineColor.a);

            return c;
        }

        static Color Gradient(in Rounded s, float px, float py)
        {
            // Угол по соглашению CSS: 0° — снизу вверх, растёт по часовой.
            // Здесь работаем в текстурных координатах, Y вниз.
            float a = s.gradientAngleDeg * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Sin(a), Mathf.Cos(a));

            float projection = px * dir.x + py * dir.y;
            float extent = Mathf.Abs(s.w * dir.x) + Mathf.Abs(s.h * dir.y);
            float origin = Mathf.Min(0f, s.w * dir.x) + Mathf.Min(0f, s.h * dir.y);

            float t = extent <= 0f ? 0f : Mathf.Clamp01((projection - origin) / extent);
            float mid = Mathf.Clamp(s.midStop <= 0f ? 0.5f : s.midStop, 0.001f, 0.999f);

            return t <= mid
                ? Color.Lerp(s.c0, s.c1, t / mid)
                : Color.Lerp(s.c1, s.c2, (t - mid) / (1f - mid));
        }

        /// <summary>Точка внутри скруглённого прямоугольника; edgeDist — расстояние до края.</summary>
        static bool Inside(in Rounded s, float px, float py, out float edgeDist)
        {
            float w = s.w, h = s.h;
            float tl = s.radii.x, tr = s.radii.y, br = s.radii.z, bl = s.radii.w;

            float cx, cy, r;

            if (px < tl && py < tl)            { cx = tl; cy = tl; r = tl; }
            else if (px > w - tr && py < tr)   { cx = w - tr; cy = tr; r = tr; }
            else if (px > w - br && py > h - br) { cx = w - br; cy = h - br; r = br; }
            else if (px < bl && py > h - bl)   { cx = bl; cy = h - bl; r = bl; }
            else
            {
                edgeDist = Mathf.Min(Mathf.Min(px, w - px), Mathf.Min(py, h - py));
                return px >= 0f && py >= 0f && px <= w && py <= h;
            }

            float d = Vector2.Distance(new Vector2(px, py), new Vector2(cx, cy));
            edgeDist = r - d;
            return d <= r;
        }

        static Color Blend(Color under, Color over, float amount)
        {
            amount = Mathf.Clamp01(amount);
            return new Color(
                Mathf.Lerp(under.r, over.r, amount),
                Mathf.Lerp(under.g, over.g, amount),
                Mathf.Lerp(under.b, over.b, amount),
                under.a);
        }

        static Color Premul(Color c, float coverage)
            => new Color(c.r, c.g, c.b, c.a * coverage);
    }
}
