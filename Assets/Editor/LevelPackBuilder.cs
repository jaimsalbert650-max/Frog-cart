using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using FrogCart.Core;
using FrogCart.Data;
using FrogCart.Runtime;

/// <summary>
/// Пак уровней Food Hunt: из ассетов просмотрщика — в наши уровни и в сцены,
/// по одной на уровень, чтобы можно было открыть и играть.
///
/// Ассеты `Level_XXXX.asset` в `Scenes` лежат от справочного проекта и ссылаются
/// на его `LevelViewer.FoodHuntLevelAsset`, скрипта которого здесь нет. Unity видит
/// их как объект с потерянным скриптом и типами не отдаёт, поэтому YAML читается
/// текстом — благо формат простой.
///
/// Семантику формата берём из исходника справочного проекта, а не на глаз
/// (`Assets/_LevelViewer/FoodHuntLevelAsset.cs`):
///
/// - `cells` — «Palette index per cell, **0 = empty**. Row-major, **origin bottom-left**»;
/// - `flags` — `Hidden = 1, Ice = 2, BigBlock = 4`.
///
/// Отсюда два расхождения со старым <see cref="FoodHuntLevelConverter"/>, который
/// писался раньше исходника и обоих правил не знал: он отображает сырой `0` в цвет 1,
/// то есть заливает пустоту блоками, и не переворачивает картинку, хотя начало
/// координат снизу. Здесь и то и другое сделано правильно; старый конвертер не
/// трогаю — он привязан к своему уровню 87 и своему пути.
/// </summary>
public static class LevelPackBuilder
{
    /// <summary>Куда лягут сцены. Отдельная папка, иначе они смешаются с исходниками.</summary>
    static string SceneDir() => $"{FrogCartPaths.Root()}/Scenes/Levels";

    /// <summary>Где лежат исходные ассеты просмотрщика.</summary>
    static string SourceDir() => $"{FrogCartPaths.Root()}/Scenes";

    /// <summary>
    /// Только уровни: ассеты `LevelData`, которые перетаскиваются на `Game3DBootstrap`
    /// в готовой сцене. Сцены при этом не плодятся — уровень ставится на существующую.
    /// </summary>
    [MenuItem("Frog Cart/Convert Level Pack To Levels")]
    public static void ConvertAll() => Run(withScenes: false);

    /// <summary>То же плюс по сцене на уровень, если нужен вход в каждый отдельно.</summary>
    [MenuItem("Frog Cart/Build Level Pack Scenes")]
    public static void BuildAll() => Run(withScenes: true);

    static void Run(bool withScenes)
    {
        var sources = Directory.GetFiles(SourceDir(), "Level_*.asset");
        System.Array.Sort(sources);

        if (sources.Length == 0)
        {
            Debug.LogWarning($"[LevelPack] В {SourceDir()} нет ни одного Level_*.asset");
            return;
        }

        // Два прохода, и это не стиль, а обход граблей из HOWTO: объект, возвращённый
        // из CreateAsset, протухает, а NewScene выгружает ассеты, на которые ещё никто
        // не ссылается. Поэтому сперва создаём все уровни и только потом, уже после
        // каждого NewScene, грузим их с диска заново.
        var built = new List<int>();
        var skipped = new List<string>();

        foreach (string source in sources)
        {
            if (TryConvert(source, out int levelId, out string note)) built.Add(levelId);
            else skipped.Add(note);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[LevelPack] Уровней собрано: {built.Count} в {FrogCartPaths.DataDir()}.");

        if (withScenes)
        {
            var scenePaths = new List<string>();
            foreach (int levelId in built) scenePaths.Add(BuildScene(levelId));

            AddToBuildSettings(scenePaths);
            Debug.Log($"[LevelPack] Сцен собрано: {scenePaths.Count} в {SceneDir()}.");
        }

        foreach (string note in skipped) Debug.LogWarning($"[LevelPack] {note}");
    }

    // ── исходник → картинка ─────────────────────────────────────────────────────

    /// <summary>Куда лягут картинки уровней.</summary>
    static string ImageDir() => $"{FrogCartPaths.Root()}/Levels";

    /// <summary>
    /// Уровни картинками — то, что кладётся в любую свою игру, а не только в эту.
    ///
    /// Формат берём у здешнего импорта (`LevelImage`), чтобы картинки заходили
    /// обратно без единой правки: пиксель на клетку, цвет ровно из палитры,
    /// прозрачный пиксель — пустая клетка. Система координат у Food Hunt и у
    /// текстур Unity одна и та же — начало снизу-слева, — поэтому переворачивать
    /// здесь ничего не нужно, в отличие от строк уровня.
    ///
    /// Цвета пишутся точными значениями палитры, а импорт ищет ближайший: значит
    /// круг «картинка → уровень → картинка» замкнут и ничего не теряет.
    /// </summary>
    [MenuItem("Frog Cart/Export Level Pack As Images")]
    public static void ExportImages()
    {
        var palette = AssetDatabase.LoadAssetAtPath<ColorPalette>(
            $"{FrogCartPaths.DataDir()}/Palette.asset");

        if (palette == null)
        {
            Debug.LogError("[LevelPack] Палитра не найдена — сначала Rebuild Assets And Scene");
            return;
        }

        var sources = Directory.GetFiles(SourceDir(), "Level_*.asset");
        System.Array.Sort(sources);

        Directory.CreateDirectory(ImageDir());

        var written = new List<string>();
        var skipped = new List<string>();

        foreach (string source in sources)
        {
            if (TryExportImage(source, palette, out string path, out string note)) written.Add(path);
            else skipped.Add(note);
        }

        AssetDatabase.Refresh();

        // Флаги импорта ставим после Refresh: до него ассета ещё нет и настраивать
        // нечего. Без Read/Write импорт не получит пиксели, а сжатие врёт в цветах
        // и роняет блоки на соседние оттенки палитры.
        foreach (string path in written) MakeReadable(path);

        Debug.Log($"[LevelPack] Картинок записано: {written.Count} в {ImageDir()}. "
                + "Кладите в свою игру или проверяйте здесь: Frog Cart → Import Level From Image.");

        foreach (string note in skipped) Debug.LogWarning($"[LevelPack] {note}");
    }

    static bool TryExportImage(string source, ColorPalette palette,
                               out string outputPath, out string note)
    {
        outputPath = null;

        string text = File.ReadAllText(source);
        string name = Path.GetFileNameWithoutExtension(source);

        int width = ReadInt(text, "width");
        int height = ReadInt(text, "height");
        string cells = ReadString(text, "cells");
        string flags = ReadString(text, "flags");

        if (width <= 0 || height <= 0 || cells.Length != width * height * 2)
        {
            note = $"{name}: не разобран или размер не сходится — картинка не записана";
            return false;
        }

        int colorCount = CountColors(cells);

        if (colorCount > palette.Count)
        {
            note = $"{name}: цветов {colorCount}, а в палитре {palette.Count}. Картинку записать "
                 + "можно, но импорт свёл бы лишние цвета к ближайшим и картинка изменилась бы — "
                 + "пропущен";
            return false;
        }

        // Строки строим тем же кодом, что и для уровней: так картинка и ассет
        // уровня заведомо описывают одно и то же, а не расходятся в мелочах.
        var rows = BuildRows(cells, flags, width, height, out _, out _, out _);

        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        var pixels = new Color32[width * height];
        var empty = new Color32(0, 0, 0, 0);

        for (int row = 0; row < height; row++)
        {
            // Строка 0 — верхняя, а у текстуры верх лежит последним.
            int y = height - 1 - row;

            for (int x = 0; x < width; x++)
            {
                int id = rows[row][x] - '0';

                pixels[y * width + x] = id == 0
                    ? empty
                    : (Color32)palette.Get(id).baseColor;
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();

        outputPath = $"{ImageDir()}/{name}.png";
        File.WriteAllBytes(outputPath, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);

        note = null;
        return true;
    }

    /// <summary>Без Read/Write и без отключённого сжатия импорт цветов не сойдётся.</summary>
    static void MakeReadable(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;

        importer.isReadable = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.filterMode = FilterMode.Point;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
    }

    // ── исходник → LevelData ────────────────────────────────────────────────────

    static bool TryConvert(string source, out int levelId, out string note)
    {
        string text = File.ReadAllText(source);
        string name = Path.GetFileNameWithoutExtension(source);

        levelId = ReadInt(text, "levelId");
        int width = ReadInt(text, "width");
        int height = ReadInt(text, "height");
        string cells = ReadString(text, "cells");
        string flags = ReadString(text, "flags");

        if (levelId <= 0 || width <= 0 || height <= 0)
        {
            note = $"{name}: не разобрана шапка (levelId/width/height)";
            return false;
        }

        if (cells.Length != width * height * 2)
        {
            note = $"{name}: клеток {cells.Length / 2}, а размер обещан {width}x{height}";
            return false;
        }

        // Цвета считаем до сборки строк. Клетка в строке уровня — одна цифра, и это
        // не настройка, а формат: девятью цветами ограничены и строки, и палитра.
        // Уровень с двенадцатью цветами нельзя «немного ужать», его надо пропустить.
        int colorCount = CountColors(cells);

        if (colorCount > GridModel.MaxColor)
        {
            note = $"{name}: цветов {colorCount}, а в строку уровня влезает "
                 + $"{GridModel.MaxColor} — пропущен";
            return false;
        }

        var rows = BuildRows(cells, flags, width, height, out var hiddenRows, out int hiddenCount,
                             out int bigBlockCount);

        var counts = LevelImage.CountByColor(rows, colorCount);

        int blocks = 0;
        for (int color = 1; color <= colorCount; color++) blocks += counts[color];

        var queue = SplitIntoCarts(counts, colorCount);

        // Контур стартует пустым — вагонетки запускает игрок. Слоты нужны заглушками,
        // иначе старая часть кода не найдёт своих пяти мест.
        var loop = new List<LevelData.CartDef>();
        while (loop.Count < 5) loop.Add(new LevelData.CartDef { colorId = 1, capacity = 0 });

        var level = ScriptableObject.CreateInstance<LevelData>();
        level.Fill(levelId, rows, loop.ToArray(), queue.ToArray());

        // Прочности в ассете просмотрщика нет вовсе: `bigHp` живёт только в JSON
        // оригинала. Пустой слой означает «везде 1», и это честнее, чем выдумать
        // прочность из флага BigBlock, который значит другое.
        level.FillLayers(null, hiddenRows);

        string output = $"{FrogCartPaths.DataDir()}/Level{levelId:0000}.asset";

        if (AssetDatabase.LoadAssetAtPath<LevelData>(output) != null)
            AssetDatabase.DeleteAsset(output);

        AssetDatabase.CreateAsset(level, output);

        string extra = bigBlockCount > 0
            ? $", клеток BigBlock {bigBlockCount} — механики для них у нас нет, взяты обычными"
            : string.Empty;

        Debug.Log($"[LevelPack] {name}: {width}x{height}, блоков {blocks}, цветов {colorCount}, "
                + $"вагонеток {queue.Count}, скрытых {hiddenCount}{extra}. Сохранено: {output}");

        note = null;
        return true;
    }

    /// <summary>
    /// Ёмкость цвета — во много мелких и разных вагонеток.
    ///
    /// Одна вагонетка на цвет с ёмкостью в 1292 куба — это не решение игрока, а
    /// конвейер: цвет нужен ровно один, брать нечего кроме него. Дробление и есть
    /// то, что делает очередь выбором — «взять на 5 или на 30».
    ///
    /// Сумма по цвету сохраняется точно. Это не аккуратность, а условие
    /// работоспособности: меньше — уровень непроходим, больше — победа наступит
    /// с недоеденной картинкой.
    ///
    /// Раскладка круговая, а не блоками по цвету: видно всегда девять штук, и
    /// если сложить их подряд одним цветом, в окне не окажется ни выбора цвета,
    /// ни выбора размера.
    /// </summary>
    static List<LevelData.CartDef> SplitIntoCarts(int[] counts, int colorCount)
    {
        // Набор номиналов задаёт всю фактуру очереди: есть на что разменять
        // остаток и есть что поставить «на подольше».
        int[] cycle = { 30, 8, 18, 5, 24, 12, 15, 6 };
        const int MinTail = 4;

        var perColor = new List<int>[colorCount + 1];
        int longest = 0;

        for (int color = 1; color <= colorCount; color++)
        {
            perColor[color] = new List<int>();
            if (counts[color] == 0) continue;

            int remaining = counts[color];

            // Фаза от номера цвета: иначе все цвета дробятся одинаково и очередь
            // читается как повтор одного узора.
            int i = color;

            while (remaining > 0)
            {
                int size = cycle[i % cycle.Length];

                // Огрызок в две-три клетки бесполезен: вагонетка уедет, не успев
                // ничего решить. Такой хвост доливается в текущую.
                if (size >= remaining || remaining - size < MinTail) size = remaining;

                perColor[color].Add(size);
                remaining -= size;
                i++;
            }

            longest = Mathf.Max(longest, perColor[color].Count);
        }

        var queue = new List<LevelData.CartDef>();

        for (int k = 0; k < longest; k++)
        for (int color = 1; color <= colorCount; color++)
        {
            if (k >= perColor[color].Count) continue;

            queue.Add(new LevelData.CartDef
            {
                colorId = color,
                capacity = perColor[color][k],
            });
        }

        return queue;
    }

    /// <summary>Сколько разных непустых индексов в клетках.</summary>
    static int CountColors(string cells)
    {
        var seen = new HashSet<int>();

        for (int i = 0; i < cells.Length; i += 2)
        {
            int raw = HexPair(cells, i);
            if (raw != 0) seen.Add(raw);
        }

        return seen.Count;
    }

    /// <summary>
    /// Картинка и слой скрытости. Строки переворачиваются: у оригинала начало
    /// координат снизу-слева, у нас — сверху-слева, и без переворота уровень встал
    /// бы вверх ногами.
    /// </summary>
    static string[] BuildRows(string cells, string flags, int width, int height,
                              out string[] hiddenRows, out int hiddenCount,
                              out int bigBlockCount)
    {
        var map = new Dictionary<int, int>();
        var rows = new string[height];
        var hidden = new string[height];

        var colorLine = new StringBuilder(width);
        var hiddenLine = new StringBuilder(width);

        bool hasFlags = flags.Length == cells.Length;
        hiddenCount = 0;
        bigBlockCount = 0;

        for (int row = 0; row < height; row++)
        {
            int y = height - 1 - row;

            colorLine.Clear();
            hiddenLine.Clear();

            for (int x = 0; x < width; x++)
            {
                int index = (y * width + x) * 2;
                int raw = HexPair(cells, index);

                if (raw == 0)
                {
                    colorLine.Append('0');
                    hiddenLine.Append('0');
                    continue;
                }

                if (!map.TryGetValue(raw, out int id))
                {
                    id = map.Count + 1;
                    map[raw] = id;
                }

                colorLine.Append((char)('0' + id));

                int flag = hasFlags ? HexPair(flags, index) : 0;
                bool isHidden = (flag & 1) != 0;

                if (isHidden) hiddenCount++;
                if ((flag & 4) != 0) bigBlockCount++;

                hiddenLine.Append(isHidden ? '1' : '0');
            }

            rows[row] = colorLine.ToString();
            hidden[row] = hiddenLine.ToString();
        }

        hiddenRows = hidden;
        return rows;
    }

    static int HexPair(string text, int index)
        => System.Convert.ToInt32(text.Substring(index, 2), 16);

    // ── LevelData → сцена ───────────────────────────────────────────────────────

    /// <summary>
    /// Сцена уровня — копия Game3D с другой ссылкой на уровень. Всё остальное
    /// строит на старте сам Game3DBootstrap, поэтому в файле сцены лежит ровно
    /// один объект, и двадцать таких сцен ничего не весят.
    /// </summary>
    static string BuildScene(int levelId)
    {
        Directory.CreateDirectory(SceneDir());

        string scenePath = $"{SceneDir()}/Level_{levelId:0000}.unity";
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Грузим строго после NewScene: до неё объекты выгружаются как «никому не
        // нужные», проверка != null у них ложна, а в сцену пишется null.
        var palette = AssetDatabase.LoadAssetAtPath<ColorPalette>($"{FrogCartPaths.DataDir()}/Palette.asset");
        var config = AssetDatabase.LoadAssetAtPath<GameConfig>($"{FrogCartPaths.DataDir()}/Config.asset");
        var level = AssetDatabase.LoadAssetAtPath<LevelData>($"{FrogCartPaths.DataDir()}/Level{levelId:0000}.asset");
        var showcase = AssetDatabase.LoadAssetAtPath<LevelData>($"{FrogCartPaths.DataDir()}/Level01.asset");

        if (palette == null || config == null || level == null)
            throw new System.InvalidOperationException(
                $"Ассеты уровня {levelId} не найдены — сначала Rebuild Assets And Scene");

        var go = new GameObject("Game3DBootstrap");
        var bootstrap = go.AddComponent<Game3DBootstrap>();
        go.AddComponent<AutoScreenshot>();

        bootstrap.EditorAssign(config, palette, level, showcase);

        if (!bootstrap.EditorHasAllRefs)
            throw new System.InvalidOperationException($"Ссылки уровня {levelId} не присвоились");

        EditorUtility.SetDirty(bootstrap);
        EditorSceneManager.MarkSceneDirty(scene);

        if (!EditorSceneManager.SaveScene(scene, scenePath))
            throw new System.InvalidOperationException($"Не удалось сохранить {scenePath}");

        return scenePath;
    }

    /// <summary>
    /// Сцены дописываются к списку сборки, а не заменяют его: Game и Game3D должны
    /// остаться, PlayMode-тесты грузят их по имени.
    /// </summary>
    static void AddToBuildSettings(List<string> scenePaths)
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        int added = 0;

        foreach (string path in scenePaths)
        {
            if (scenes.Exists(s => s.path == path)) continue;

            scenes.Add(new EditorBuildSettingsScene(path, true));
            added++;
        }

        if (added == 0) return;

        EditorBuildSettings.scenes = scenes.ToArray();
        Debug.Log($"[LevelPack] В список сборки добавлено сцен: {added}");
    }

    static int ReadInt(string text, string field)
    {
        var match = Regex.Match(text, $@"^\s*{field}:\s*(-?\d+)\s*$", RegexOptions.Multiline);
        return match.Success ? int.Parse(match.Groups[1].Value) : 0;
    }

    static string ReadString(string text, string field)
    {
        var match = Regex.Match(text, $@"^\s*{field}:\s*(\S+)\s*$", RegexOptions.Multiline);
        return match.Success ? match.Groups[1].Value : string.Empty;
    }
}
