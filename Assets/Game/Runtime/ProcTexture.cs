using UnityEngine;
using System.Collections.Generic;

namespace FrogCart.Runtime
{
    /// <summary>
    /// Текстуры, сделанные кодом. Пока нужна одна — древесина стола.
    ///
    /// На референсе стол не заливка, а доска с волокном: тонкие тёплые полосы
    /// разной насыщенности, слегка «плывущие» по длине. Ровная коричневая
    /// плоскость рядом с этим читается как фон, а не как поверхность.
    ///
    /// Шум здесь детерминированный: seed фиксирован, поэтому стол выглядит
    /// одинаково в редакторе, в плеере и на скриншоте для сверки.
    /// </summary>
    public static class ProcTexture
    {
        static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();

        /// <summary>
        /// Древесина: полосы волокна вдоль V с редкими более тёмными прожилками.
        /// </summary>
        public static Texture2D Wood(Color light, Color dark, string key, int size = 512)
        {
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: true)
            {
                name = key,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4,
            };

            var random = new System.Random(20260803);

            // Плахи: стол собран из досок, между ними тёмный шов. Без швов текстура
            // читается как один бесконечный лист шпона.
            const int Planks = 4;

            var plankTint = new float[Planks];
            for (int i = 0; i < Planks; i++) plankTint[i] = -0.08f + (float)random.NextDouble() * 0.16f;

            // Волокно — сумма нескольких синусоид разной частоты. Одна даёт
            // полосатый матрас, три уже читаются как неровное дерево.
            int veinCount = 7;
            var veinX = new float[veinCount];
            var veinWidth = new float[veinCount];
            var veinDepth = new float[veinCount];

            for (int i = 0; i < veinCount; i++)
            {
                veinX[i] = (float)random.NextDouble();
                veinWidth[i] = 0.004f + (float)random.NextDouble() * 0.012f;
                veinDepth[i] = 0.10f + (float)random.NextDouble() * 0.18f;
            }

            var pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size;
                float v = y / (float)size;

                // Увод волокна по длине доски. Он должен быть еле заметным: при 0.012
                // полосы змеились, и стол читался как вода, а не как дерево.
                float drift = Mathf.Sin(v * Mathf.PI * 2f) * 0.0015f
                            + Mathf.Sin(v * Mathf.PI * 6.3f + 1.7f) * 0.0008f;

                float grain =
                      Mathf.Sin((u + drift) * Mathf.PI * 2f * 34f) * 0.5f
                    + Mathf.Sin((u + drift) * Mathf.PI * 2f * 81f + 2.1f) * 0.3f
                    + Mathf.Sin((u + drift) * Mathf.PI * 2f * 173f + 0.8f) * 0.2f;

                float t = 0.5f + grain * 0.09f;

                // Прожилки: узкие тёмные полосы поверх общего волокна.
                for (int i = 0; i < veinCount; i++)
                {
                    float distance = Mathf.Abs(Mathf.Repeat(u + drift - veinX[i] + 0.5f, 1f) - 0.5f);
                    if (distance >= veinWidth[i]) continue;

                    float falloff = 1f - distance / veinWidth[i];
                    t += falloff * falloff * veinDepth[i];
                }

                // Плаха, в которую попал пиксель: своя лёгкая подтонировка.
                float plankPos = u * Planks;
                int plank = Mathf.Clamp((int)plankPos, 0, Planks - 1);
                t += plankTint[plank];

                // Шов: узкая тёмная щель на стыке плах.
                float toSeam = Mathf.Abs(plankPos - Mathf.Round(plankPos));
                if (toSeam < 0.015f) t += (1f - toSeam / 0.015f) * 0.5f;

                pixels[y * size + x] = Color.Lerp(light, dark, Mathf.Clamp01(t));
            }

            texture.SetPixels32(pixels);
            texture.Apply(updateMipmaps: true);

            Cache[key] = texture;
            return texture;
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
    }
}
