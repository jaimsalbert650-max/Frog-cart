using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using FrogCart.Core;
using FrogCart.Data;

/// <summary>
/// Конвертер уровня из распакованного JSON Food Hunt.
///
/// Отличие от <see cref="FoodHuntLevelConverter"/>: тот читает ассет просмотрщика,
/// где от клетки остался только цвет. В JSON лежит вся клетка целиком — вместе
/// с `bigHp` и `isHidden`, то есть с механиками. Ради них конвертер и написан:
/// прочные и скрытые клетки берутся из данных оригинала, а не расставляются
/// наугад.
///
/// Разбор регулярками, а не JsonUtility, по двум причинам. Во-первых, схема
/// оригинала тянет за собой десяток вложенных типов, из которых нужны четыре
/// поля. Во-вторых, файл на 450 КБ и 1845 клеток разбирается один раз в редакторе,
/// поэтому скорость не важна вовсе.
/// </summary>
public static class FoodHuntJsonConverter
{
    const string JsonDir =
        @"D:\Unity Projects\pixel Puzzles\_reverse\extracted\levels_json";



    /// <summary>
    /// Что собирается по кнопке.
    ///
    /// **106** выбран не случайно: в нём есть обе механики сразу — 71 скрытая
    /// клетка и 21 прочная, — и всего семь цветов, а палитра у нас на восемь.
    /// Уровни с обеими механиками встречаются редко: на первых двухстах это
    /// 81, 106, 109, 116, 123, 161 и 246.
    ///
    /// **87** пришёл сюда позже и ради очереди. Он был сделан старым
    /// <see cref="FoodHuntLevelConverter"/>, а тот выдаёт по вагонетке на цвет:
    /// восемь штук на 1225 мест, самая большая на 242. Через этот конвертер он
    /// получает дроблёную и разложенную по цветам очередь, как весь остальной пак.
    ///
    /// Его 1225 блоков на доске 35x35 — ровно все клетки до единой — это не
    /// ошибка конвертера, а сам уровень: в JSON все 1225 клеток `isActive`.
    /// Пустых клеток на нём нет, поэтому и путаница старого конвертера с
    /// «`0` значит пусто» его никогда не касалась.
    /// </summary>
    static readonly int[] DefaultLevels = { 87, 106 };

    [MenuItem("Frog Cart/Convert Food Hunt Levels With Mechanics")]
    public static void ConvertDefault()
    {
        foreach (int levelId in DefaultLevels) Convert(levelId);
    }

    public static void Convert(int levelId)
    {
        string path = Path.Combine(JsonDir, $"{levelId}.json");

        if (!File.Exists(path))
        {
            Debug.LogError($"[FoodHuntJson] Нет файла: {path}. "
                         + "Восстановить: _reverse/tools/decode_levels.ps1");
            return;
        }

        string text = File.ReadAllText(path);

        if (!TryReadSize(text, out int width, out int height))
        {
            Debug.LogError("[FoodHuntJson] Не разобран levelSize");
            return;
        }

        var cells = ParseCells(text);

        if (cells.Count != width * height)
        {
            Debug.LogError($"[FoodHuntJson] Клеток {cells.Count}, "
                         + $"а размер обещан {width}x{height}");
            return;
        }

        var picture = new string[height];
        var hpRows = new string[height];
        var hiddenRows = new string[height];

        var order = new List<int>();
        var map = new Dictionary<int, int>();

        var colorLine = new StringBuilder(width);
        var hpLine = new StringBuilder(width);
        var hiddenLine = new StringBuilder(width);

        for (int y = 0; y < height; y++)
        {
            colorLine.Clear();
            hpLine.Clear();
            hiddenLine.Clear();

            // Начало координат у оригинала снизу-слева, у наших строк уровня —
            // сверху-слева, поэтому ряд берётся с конца.
            //
            // Без этого картинка встаёт вверх ногами, и заметить это тяжелее,
            // чем кажется: пиксель-арт симметричнее, чем помнится, а доска
            // и без того непривычная. На 106-м уровне это всадник на вздыбленной
            // лошади, и полоса тени из-под копыт полтора месяца висела над ним
            // тучей. Правило записано в исходнике просмотрщика
            // (`FoodHuntLevelAsset.cs`: «origin bottom-left») и учтено
            // в LevelPackBuilder — здесь оно было потеряно.
            int sourceY = height - 1 - y;

            for (int x = 0; x < width; x++)
            {
                var cell = cells[sourceY * width + x];

                if (!cell.Active)
                {
                    colorLine.Append('0');
                    hpLine.Append('0');
                    hiddenLine.Append('0');
                    continue;
                }

                if (!map.TryGetValue(cell.Color, out int id))
                {
                    order.Add(cell.Color);
                    id = order.Count;
                    map[cell.Color] = id;
                }

                colorLine.Append((char)('0' + id));

                // Прочность оригинала доходит до 10, а у нас на клетку одна цифра.
                // Девять ударов по одной клетке и так за гранью терпения игрока.
                int hp = Mathf.Clamp(cell.Hp, 1, CellLayers.MaxHp);
                hpLine.Append((char)('0' + hp));

                hiddenLine.Append(cell.Hidden ? '1' : '0');
            }

            picture[y] = colorLine.ToString();
            hpRows[y] = hpLine.ToString();
            hiddenRows[y] = hiddenLine.ToString();
        }

        if (order.Count > GridModel.MaxColor)
        {
            Debug.LogError($"[FoodHuntJson] Цветов {order.Count}, "
                         + $"а поддерживается {GridModel.MaxColor}");
            return;
        }

        Save(levelId, picture, hpRows, hiddenRows, cells, map, order.Count);
    }

    struct Cell
    {
        public bool Active;
        public int Color;
        public int Hp;
        public bool Hidden;
    }

    /// <summary>
    /// Расстановка механик вагонеток.
    ///
    /// **Здесь я не переношу данные оригинала, а расставляю сам, и это надо знать.**
    /// Причина в том, что вагонетки у нас не те же, что коробки Food Hunt: их
    /// ёмкости считаются от числа ударов по нашей картинке, иначе уровень был бы
    /// непроходим. Сопоставить наши пять вагонеток с их сорока коробками нечем,
    /// а значит и `frozenCount` с `combineTarget` перенести не на что.
    ///
    /// Правила самой механики взяты из оригинала, расстановка — детерминированная
    /// и одинаковая при каждом прогоне, чтобы уровень не менялся между сборками.
    /// </summary>
    static void MarkCartMechanics(List<LevelData.CartDef> loop, List<LevelData.CartDef> queue)
    {
        // Связка — голова очереди и её хвост, а не две вагонетки через одну.
        //
        // Расстановка здесь решает, проходим уровень или нет, и это не фигура
        // речи. Связка отнимает ёмкость ровно тогда, когда обе вагонетки пары
        // стоят на контуре: опустела одна — вторую уводит с недоеденным
        // остатком. Запаса мест нет ни у одного цвета, значит любой такой
        // остаток — проигрыш.
        //
        // Через одну (как было) обе уезжали на контур первым же движением:
        // мест четыре, а пара стоит на первом и третьем. Срабатывание было не
        // риском, а расписанием — на 106-м уровне партия кончалась на 618
        // блоках из 735.
        //
        // Через всю очередь пара физически не встречается: голова опустеет за
        // четыре укуса и уедет задолго до того, как хвост подойдёт к запуску.
        // Связка при этом срабатывает вхолостую — партнёра на контуре нет, —
        // и уровень проходится расстановкой, без всякой помощи от правил.
        //
        // Оговорка одна: очередь берётся с любого места, и игрок вправе
        // вытащить хвост на контур руками, пока голова ещё жива. Тогда пара
        // сработает по-настоящему. Именно этот случай закрывает возврат мест
        // (GameController.ReturnSeatsToQueue) — расстановка снимает правило,
        // возврат снимает исключение из него.
        if (queue.Count >= 3)
        {
            int tail = queue.Count - 1;

            var head = queue[0];
            var last = queue[tail];
            head.linkGroup = 1;
            last.linkGroup = 1;
            queue[0] = head;
            queue[tail] = last;
        }

        // Замороженной делается не первая вагонетка, а вторая: первая нужна
        // рабочей, иначе самый первый тап игрока упирается в лёд.
        if (queue.Count >= 2)
        {
            var frozen = queue[1];
            frozen.frozenCount = 3;
            queue[1] = frozen;
        }
    }

    static bool TryReadSize(string text, out int width, out int height)
    {
        var match = Regex.Match(text,
            @"""levelSize""\s*:\s*\{\s*""x""\s*:\s*([0-9.]+)\s*,\s*""y""\s*:\s*([0-9.]+)");

        width = height = 0;
        if (!match.Success) return false;

        width = Mathf.RoundToInt(float.Parse(match.Groups[1].Value,
            System.Globalization.CultureInfo.InvariantCulture));
        height = Mathf.RoundToInt(float.Parse(match.Groups[2].Value,
            System.Globalization.CultureInfo.InvariantCulture));

        return width > 0 && height > 0;
    }

    /// <summary>
    /// Клетки идут в `listBrickToolItemInfo` подряд, строка за строкой. Порядок
    /// тот же, что в hex-строке ассета просмотрщика: index = y * width + x.
    /// </summary>
    static List<Cell> ParseCells(string text)
    {
        var cells = new List<Cell>();

        var pattern = new Regex(
            @"\{""id"":\d+,""color"":(\d+),""isActive"":(true|false),""isHidden"":(true|false)"
          + @",""isKey"":(?:true|false),""isIce"":(?:true|false),""bigHp"":(\d+)");

        foreach (Match match in pattern.Matches(text))
        {
            cells.Add(new Cell
            {
                Color = int.Parse(match.Groups[1].Value),
                Active = match.Groups[2].Value == "true",
                Hidden = match.Groups[3].Value == "true",
                // Четвёртая, а не пятая: isKey и isIce взяты некапчурящими группами.
                Hp = int.Parse(match.Groups[4].Value),
            });
        }

        return cells;
    }

    /// <summary>
    /// Ёмкости вагонеток считаются по числу **ударов**, а не блоков.
    ///
    /// Это не мелочь: прочная клетка на десять жизней съедает десять мест в
    /// вагонетке. Посчитай по блокам — и уровень станет непроходимым ровно на
    /// тех клетках, ради которых механику и добавляли.
    /// </summary>
    static void Save(int levelId, string[] picture, string[] hpRows, string[] hiddenRows,
                     List<Cell> cells, Dictionary<int, int> colorMap, int colorCount)
    {
        var hits = new int[colorCount + 1];
        int blocks = 0;

        foreach (var cell in cells)
        {
            if (!cell.Active) continue;

            int id = colorMap[cell.Color];
            hits[id] += Mathf.Clamp(cell.Hp, 1, CellLayers.MaxHp);
            blocks++;
        }

        var loop = new List<LevelData.CartDef>();

        // Все вагонетки уходят в очередь, контур остаётся пустым.
        //
        // Игрок сам решает, кого и когда запустить, — в этом теперь игра. Раньше
        // первые пять ставились на контур сразу, и половина уровня разбиралась
        // без единого его решения.
        //
        // Дробление то же, что у пака (LevelPackBuilder.SplitIntoCarts), а не по
        // одной вагонетке на цвет, как было здесь раньше. Одна вагонетка на цвет —
        // это не выбор, а конвейер: на 106-м их выходило семь на 903 места, самая
        // большая на 298, и всё, что оставалось игроку, — запускать единственную
        // подходящую. Соседние уровни пака при том же размере картинки дают за сотню
        // вагонеток по 3–14 мест, и очередь у них спрашивает «какой цвет и надолго ли».
        //
        // Второй довод — цена ошибки. Место, ушедшее с вагонеткой (связка уводит
        // партнёра непустым), на дроблёном уровне стоит остаток мелкой вагонетки,
        // а не двести с лишним мест. Само по себе это уровень не спасает — запаса
        // мест нет ни у одного цвета, — поэтому места и возвращаются в очередь
        // (GameController.ReturnSeatsToQueue). Дробление лишь снимает случай,
        // когда одна потерянная вагонетка уносит четверть картинки.
        var queue = LevelPackBuilder.SplitIntoCarts(hits, colorCount);

        while (loop.Count < 5) loop.Add(new LevelData.CartDef { colorId = 1, capacity = 0 });

        MarkCartMechanics(loop, queue);

        var level = ScriptableObject.CreateInstance<LevelData>();
        level.Fill(levelId, picture, loop.ToArray(), queue.ToArray());
        level.FillLayers(hpRows, hiddenRows);

        string output = $"{FrogCartPaths.DataDir()}/Level{levelId:0000}.asset";

        if (AssetDatabase.LoadAssetAtPath<LevelData>(output) != null)
            AssetDatabase.DeleteAsset(output);

        AssetDatabase.CreateAsset(level, output);
        AssetDatabase.SaveAssets();

        int armoured = 0, hidden = 0;
        foreach (var cell in cells)
        {
            if (!cell.Active) continue;
            if (cell.Hp > 1) armoured++;
            if (cell.Hidden) hidden++;
        }

        Debug.Log($"[FoodHuntJson] Уровень {levelId}: блоков {blocks}, цветов {colorCount}, "
                + $"прочных {armoured}, скрытых {hidden}. Сохранено: {output}");
    }
}
