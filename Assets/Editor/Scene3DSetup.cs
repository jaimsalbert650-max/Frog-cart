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
    const string DataDir = "Assets/Game/Data";
    const string ScenePath = "Assets/Scenes/Game3D.unity";

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
        Directory.CreateDirectory("Assets/Scenes");
        EnsureStandardShaderIsIncluded();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Загрузка после NewScene: иначе ассеты выгружаются как «никому не нужные»
        // и в сцену пишется null.
        var palette = AssetDatabase.LoadAssetAtPath<ColorPalette>($"{DataDir}/Palette.asset");
        var config = AssetDatabase.LoadAssetAtPath<GameConfig>($"{DataDir}/Config.asset");
        // Основной уровень объёмной сцены — настоящий уровень Food Hunt: 35x35,
        // 1225 блоков, 8 цветов. Учебный шар 14x16 остался витринным.
        //
        // Причина не в содержании картинки, а в плотности: у оригинала пиксель-арт
        // в сотни клеток, и вся подача держится на нём. Четырнадцать крупных кирпичей
        // на строку читаются как конструктор, а не как изображение.
        var level = AssetDatabase.LoadAssetAtPath<LevelData>($"{DataDir}/Level0087.asset");
        var showcase = AssetDatabase.LoadAssetAtPath<LevelData>($"{DataDir}/Level01.asset");

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

        if (!EditorSceneManager.SaveScene(scene, ScenePath))
            throw new System.InvalidOperationException($"Не удалось сохранить {ScenePath}");

        // Сцена добавляется к списку сборки, а не заменяет его: плоская версия должна
        // остаться собираемой, и PlayMode-тесты грузят обе по имени.
        var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(
            EditorBuildSettings.scenes);

        if (!scenes.Exists(s => s.path == ScenePath))
        {
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        Debug.Log($"[Scene3D] Сцена сохранена: {ScenePath}");
    }

    [MenuItem("Frog Cart/Build 3D Player")]
    public static void BuildPlayer()
    {
        const string OutputDir = "Build/Windows3D";
        Directory.CreateDirectory(OutputDir);

        var options = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
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
