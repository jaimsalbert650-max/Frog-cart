using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using FrogCart.Core;
using FrogCart.Data;

/// <summary>
/// Конвертер уровня из разобранного Food Hunt в формат этой игры.
///
/// Читает YAML ассета `FoodHuntLevelAsset` напрямую текстом, а не через типы рипа:
/// связываться с Assembly-CSharp справочного проекта незачем, а формат простой —
/// width, height и hex-строка клеток по два символа на клетку.
///
/// Цвета Food Hunt (их BindingColor) сопоставляются моим по порядку первого появления:
/// точных RGB оригинала у нас нет, да и палитра здесь своя.
/// </summary>
public static class FoodHuntLevelConverter
{
    const string SourceAsset =
        @"D:\Unity Projects\pixel Puzzles\Assets\_LevelViewer\Resources\Levels\Level_0087.asset";



    [MenuItem("Frog Cart/Convert Food Hunt Level 87")]
    public static void Convert()
    {
        if (!File.Exists(SourceAsset))
        {
            Debug.LogError($"[FoodHunt] Исходный ассет не найден: {SourceAsset}");
            return;
        }

        string text = File.ReadAllText(SourceAsset);

        int width = ReadInt(text, "width");
        int height = ReadInt(text, "height");
        string hex = ReadString(text, "cells");

        if (hex.Length != width * height * 2)
        {
            Debug.LogError($"[FoodHunt] Клеток {hex.Length / 2}, а размер обещан {width}x{height}");
            return;
        }

        var rows = BuildRows(hex, width, height, out var order);

        var counts = LevelImage.CountByColor(rows, order.Count);
        int total = 0;
        for (int i = 1; i <= order.Count; i++) total += counts[i];

        if (order.Count > GridModel.MaxColor)
        {
            Debug.LogError($"[FoodHunt] Цветов {order.Count}, а поддерживается {GridModel.MaxColor}");
            return;
        }

        SaveLevel(rows, counts, order.Count);

        Debug.Log($"[FoodHunt] Уровень 87: {width}x{height}, блоков {total}, цветов {order.Count}.");
    }

    /// <summary>Строки уровня и список исходных цветов в порядке первого появления.</summary>
    static string[] BuildRows(string hex, int width, int height, out List<int> order)
    {
        order = new List<int>();
        var map = new Dictionary<int, int>();

        var rows = new string[height];
        var buffer = new char[width];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = (y * width + x) * 2;
                int raw = System.Convert.ToInt32(hex.Substring(index, 2), 16);

                if (!map.TryGetValue(raw, out int id))
                {
                    order.Add(raw);
                    id = order.Count;
                    map[raw] = id;
                }

                buffer[x] = (char)('0' + id);
            }

            rows[y] = new string(buffer);
        }

        return rows;
    }

    /// <summary>
    /// Вагонетки: по одной на цвет, ёмкость ровно под число блоков этого цвета.
    /// Первые пять встают на контур, остальные ждут в очереди — так уровень
    /// заведомо сходится по ёмкости, а очередь крутится сама.
    /// </summary>
    static void SaveLevel(string[] rows, int[] counts, int colorCount)
    {
        var loop = new List<LevelData.CartDef>();
        var queue = new List<LevelData.CartDef>();

        for (int color = 1; color <= colorCount; color++)
        {
            if (counts[color] == 0) continue;

            var def = new LevelData.CartDef { colorId = color, capacity = counts[color] };
            if (loop.Count < 5) loop.Add(def);
            else queue.Add(def);
        }

        while (loop.Count < 5) loop.Add(new LevelData.CartDef { colorId = 1, capacity = 0 });

        var level = ScriptableObject.CreateInstance<LevelData>();
        level.Fill(87, rows, loop.ToArray(), queue.ToArray());

        if (AssetDatabase.LoadAssetAtPath<LevelData>($"{FrogCartPaths.DataDir()}/Level0087.asset") != null)
            AssetDatabase.DeleteAsset($"{FrogCartPaths.DataDir()}/Level0087.asset");

        AssetDatabase.CreateAsset(level, $"{FrogCartPaths.DataDir()}/Level0087.asset");
        AssetDatabase.SaveAssets();

        Debug.Log($"[FoodHunt] Сохранено: {$"{FrogCartPaths.DataDir()}/Level0087.asset"}. На контуре {loop.Count}, в очереди {queue.Count}.");
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
