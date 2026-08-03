using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using FrogCart.Data;
using FrogCart.Runtime;

/// <summary>
/// Сборка объёмной сцены. Отдельная от плоской: обе живут рядом, чтобы можно было
/// сравнить и чтобы работающая версия не ломалась, пока новая догоняет.
/// </summary>
public static class Scene3DSetup
{
    /// <summary>
    /// Пути ищутся, а не задаются константами.
    ///
    /// Этот файл живёт в двух проектах сразу: в «Frog Cart», где игра лежит прямо
    /// в `Assets/Game`, и в справочном «pixel Puzzles», где она целиком убрана
    /// в `Assets/_FrogCart`. Константы верны ровно для одного из них, и при
    /// синхронизации файла второй молча ломался: `Build 3D Scene` падал с
    /// «Ассеты не найдены», сцена оставалась старой, а выглядело это так, будто
    /// правки кода не действуют.
    ///
    /// Опорная точка — сам ассет конфига: он в проекте один, и по его папке
    /// восстанавливается вся раскладка.
    /// </summary>
    static string DataDir()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:GameConfig"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (AssetDatabase.LoadAssetAtPath<GameConfig>(path) == null) continue;

            return Path.GetDirectoryName(path).Replace('\\', '/');
        }

        return "Assets/Game/Data";
    }

    /// <summary>
    /// Путь объёмной сцены. Если рядом уже лежит сцена с «3D» в имени — пишем
    /// в неё, а не заводим вторую: у пользователя открыта именно она, и новый
    /// файл рядом означал бы, что он продолжает смотреть на старую.
    /// </summary>
    static string ScenePath()
    {
        // DataDir — это <корень>/Game/Data, значит корень на два уровня выше.
        string root = Path.GetDirectoryName(Path.GetDirectoryName(DataDir())).Replace('\\', '/');
        string sceneDir = $"{root}/Scenes";

        if (AssetDatabase.IsValidFolder(sceneDir))
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { sceneDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path).Contains("3D")) return path;
            }
        }

        return $"{sceneDir}/Game3D.unity";
    }

    /// <summary>
    /// Standard создаётся кодом через Shader.Find, а в билде такие шейдеры вырезаются:
    /// на них не ссылается ни один материал в сцене, и сборщик считает их ненужными.
    /// В плеере Shader.Find вернул бы null, и вся объёмная сцена падала бы на первом
    /// же материале. Поэтому шейдер прописывается в always-included.
    /// </summary>
    static void EnsureStandardShaderIsIncluded()
    {
        var graphics = AssetDatabase.LoadAssetAtPath<Object>("ProjectSettings/GraphicsSettings.asset");
        var serialized = new SerializedObject(graphics);
        var list = serialized.FindProperty("m_AlwaysIncludedShaders");

        foreach (string name in new[] { "Standard", "Sprites/Default" })
        {
            var shader = Shader.Find(name);
            if (shader == null)
            {
                Debug.LogError($"[Scene3D] Шейдер {name} не найден даже в редакторе.");
                continue;
            }

            bool already = false;
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == shader) already = true;

            if (already) continue;

            list.InsertArrayElementAtIndex(list.arraySize);
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = shader;
            Debug.Log($"[Scene3D] Шейдер {name} добавлен в always-included.");
        }

        serialized.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();
    }

    [MenuItem("Frog Cart/Build 3D Scene")]
    public static void BuildScene()
    {
        var scenePath = ScenePath();
        Directory.CreateDirectory(Path.GetDirectoryName(scenePath));
        EnsureStandardShaderIsIncluded();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Загрузка после NewScene: иначе ассеты выгружаются как «никому не нужные»
        // и в сцену пишется null.
        var palette = AssetDatabase.LoadAssetAtPath<ColorPalette>($"{DataDir()}/Palette.asset");
        var config = AssetDatabase.LoadAssetAtPath<GameConfig>($"{DataDir()}/Config.asset");
        // Основной уровень объёмной сцены — настоящий уровень Food Hunt 106:
        // 735 блоков, 7 цветов, и в нём есть обе механики оригинала сразу —
        // 71 скрытая клетка и 21 прочная. Учебный шар 14x16 остался витринным.
        //
        // Причина не в содержании картинки, а в плотности: у оригинала пиксель-арт
        // в сотни клеток, и вся подача держится на нём. Четырнадцать крупных кирпичей
        // на строку читаются как конструктор, а не как изображение.
        var level = AssetDatabase.LoadAssetAtPath<LevelData>($"{DataDir()}/Level0106.asset");
        var showcase = AssetDatabase.LoadAssetAtPath<LevelData>($"{DataDir()}/Level01.asset");

        if (palette == null || config == null || level == null)
            throw new System.InvalidOperationException("Ассеты не найдены — сначала Rebuild Assets And Scene");

        var go = new GameObject("Game3DBootstrap");
        var bootstrap = go.AddComponent<Game3DBootstrap>();
        go.AddComponent<AutoScreenshot>();

        bootstrap.EditorAssign(config, palette, level, showcase);

        if (!bootstrap.EditorHasAllRefs)
            throw new System.InvalidOperationException("Ссылки не присвоились");

        EditorUtility.SetDirty(bootstrap);
        EditorSceneManager.MarkSceneDirty(scene);

        if (!EditorSceneManager.SaveScene(scene, scenePath))
            throw new System.InvalidOperationException($"Не удалось сохранить {scenePath}");

        // Сцена добавляется к списку сборки, а не заменяет его: плоская версия должна
        // остаться собираемой, и PlayMode-тесты грузят обе по имени.
        var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(
            EditorBuildSettings.scenes);

        if (!scenes.Exists(s => s.path == scenePath))
        {
            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        Debug.Log($"[Scene3D] Сцена сохранена: {scenePath}");
    }

    [MenuItem("Frog Cart/Build 3D Player")]
    public static void BuildPlayer()
    {
        const string OutputDir = "Build/Windows3D";
        Directory.CreateDirectory(OutputDir);

        var options = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath() },
            locationPathName = OutputDir + "/FrogCart3D.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.Development,
        };

        var report = BuildPipeline.BuildPlayer(options);
        var summary = report.summary;

        Debug.Log($"[Scene3D] Сборка: {summary.result}, " +
                  $"размер {summary.totalSize / 1024 / 1024} МБ, ошибок {summary.totalErrors}");

        if (summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            throw new System.InvalidOperationException($"Сборка не удалась: {summary.result}");
    }
}
