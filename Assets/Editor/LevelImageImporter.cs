using System.Text;
using UnityEditor;
using UnityEngine;
using FrogCart.Core;
using FrogCart.Data;

/// <summary>
/// Импорт картинки в строки уровня: выделяешь PNG в проекте и жмёшь пункт меню.
/// Строки печатаются в консоль и кладутся в буфер обмена, оттуда их можно вставить
/// в LevelData или в FrogCartSetup.
///
/// Пайплайн из docs/unity-spec: художник рисует картинку, дизайнер расставляет поверх
/// вагонетки и очередь. Сопоставление цветов живёт в ядре (LevelImage) и покрыто
/// тестами; здесь только чтение текстуры и отчёт.
/// </summary>
public static class LevelImageImporter
{
    static string PalettePath => $"{FrogCartPaths.DataDir()}/Palette.asset";

    [MenuItem("Frog Cart/Import Level From Image %#i")]
    public static void ImportSelected()
    {
        var texture = Selection.activeObject as Texture2D;
        if (texture == null)
        {
            Debug.LogError("[LevelImage] Выдели в проекте текстуру PNG и повтори.");
            return;
        }

        var palette = AssetDatabase.LoadAssetAtPath<ColorPalette>(PalettePath);
        if (palette == null)
        {
            Debug.LogError($"[LevelImage] Палитра не найдена: {PalettePath}");
            return;
        }

        if (!EnsureReadable(texture, out string path)) return;

        var colors = new Color32[palette.Count];
        for (int i = 0; i < colors.Length; i++) colors[i] = palette.Get(i + 1).baseColor;

        string[] rows;
        try
        {
            rows = LevelImage.ToRows(texture.GetPixels32(), texture.width, texture.height, colors);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[LevelImage] {e.Message}");
            return;
        }

        Report(texture, path, rows, colors.Length);
    }

    /// <summary>
    /// Без Read/Write текстура не отдаёт пиксели. Флаг ставится один раз и остаётся:
    /// это импорт-настройка ассета, а не временное состояние.
    /// </summary>
    static bool EnsureReadable(Texture2D texture, out string path)
    {
        path = AssetDatabase.GetAssetPath(texture);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;

        if (importer == null)
        {
            Debug.LogError($"[LevelImage] Это не импортируемая текстура: {path}");
            return false;
        }

        if (importer.isReadable && importer.textureCompression == TextureImporterCompression.Uncompressed)
            return true;

        importer.isReadable = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.filterMode = FilterMode.Point;
        importer.SaveAndReimport();

        Debug.Log($"[LevelImage] Текстуре включены Read/Write и отключено сжатие: {path}. " +
                  "Сжатие врёт в цветах, и блоки прилипали бы к соседним оттенкам.");
        return true;
    }

    static void Report(Texture2D texture, string path, string[] rows, int colorCount)
    {
        var counts = LevelImage.CountByColor(rows, colorCount);
        int total = 0;
        for (int i = 1; i <= colorCount; i++) total += counts[i];

        var text = new StringBuilder();
        text.AppendLine($"// {System.IO.Path.GetFileName(path)} — {texture.width}x{texture.height}, блоков {total}");
        foreach (string row in rows) text.AppendLine($"\"{row}\",");

        EditorGUIUtility.systemCopyBuffer = text.ToString();

        var summary = new StringBuilder();
        summary.AppendLine($"[LevelImage] {texture.width}x{texture.height}, блоков {total}. Строки в буфере обмена.");
        for (int i = 1; i <= colorCount; i++)
            if (counts[i] > 0) summary.AppendLine($"   цвет {i}: {counts[i]}");

        if (texture.width != 14 || texture.height != 16)
            summary.AppendLine($"   ВНИМАНИЕ: спека ждёт 14x16, а тут {texture.width}x{texture.height} — " +
                               "раскладка сетки под другой размер не считана.");

        Debug.Log(summary.ToString() + "\n" + text);
    }
}
