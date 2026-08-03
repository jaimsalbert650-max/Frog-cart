using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using FrogCart.Data;
using FrogCart.Runtime;

/// <summary>
/// Создание ассетов и сцены из чисел спеки. Запускается в batchmode:
///   Unity.exe -batchmode -quit -projectPath "..." -executeMethod FrogCartSetup.BuildAll
///
/// Ручная сборка в инспекторе не воспроизводима: пересобрать её после правки спеки нельзя,
/// а этот метод переписывает всё за один прогон.
/// </summary>
public static class FrogCartSetup
{
    // Пути через FrogCartPaths: жёсткие константы верны только для одной
    // раскладки и молча ломают вторую при синхронизации файла между проектами.
    static string DataDir => FrogCartPaths.DataDir();
    static string SceneDir => $"{FrogCartPaths.Root()}/Scenes";

    /// <summary>
    /// Плоская сцена. Если рядом уже лежит сцена без «3D» в имени — пишем в неё,
    /// чтобы не заводить вторую рядом с той, что открыта у пользователя.
    /// </summary>
    static string ScenePath
    {
        get
        {
            if (AssetDatabase.IsValidFolder(SceneDir))
            {
                foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { SceneDir }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!System.IO.Path.GetFileNameWithoutExtension(path).Contains("3D"))
                        return path;
                }
            }

            return $"{SceneDir}/Game.unity";
        }
    }

    // Уровень 1 «воздушный шар» — docs/unity-spec/03-level-data.md, дословно.
    static readonly string[] Level1Rows =
    {
        "00000055000000",
        "00000322300000",
        "00034322343000",
        "00234322343200",
        "00234322343200",
        "00234322343200",
        "00234322343200",
        "00034322343000",
        "00004322340000",
        "00000322300000",
        "00000022000000",
        "00000100100000",
        "00000111100000",
        "00000111100000",
        "00000111100000",
        "00000000000000",
    };

    [MenuItem("Frog Cart/Rebuild Assets And Scene")]
    public static void BuildAll()
    {
        Directory.CreateDirectory(DataDir);
        Directory.CreateDirectory(SceneDir);

        CreatePalette();
        CreateConfig();
        CreateLevel("Level01", 1, QueueNormal());
        CreateLevel("Level01_LoseTest", 1, QueueLoseTest());

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[FrogCartSetup] Ассеты созданы. Блоков на уровне: {CountBlocks()}");

        BuildScene();

        Debug.Log($"[FrogCartSetup] Сцена сохранена: {ScenePath}");
    }

    static int CountBlocks()
    {
        int count = 0;
        foreach (string row in Level1Rows)
            foreach (char c in row)
                if (c != '0') count++;
        return count;
    }

    /// <summary>
    /// Сборка Windows-плеера. Запускается так же, через -executeMethod.
    /// Нужна не ради дистрибутива, а ради единственного способа увидеть игру:
    /// плеер с ключом -autoshot делает снимок экрана и выходит.
    /// </summary>
    [MenuItem("Frog Cart/Build Windows Player")]
    public static void BuildWindowsPlayer()
    {
        const string OutputDir = "Build/Windows";
        Directory.CreateDirectory(OutputDir);

        var options = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = OutputDir + "/FrogCart.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.Development,
        };

        var report = BuildPipeline.BuildPlayer(options);
        var summary = report.summary;

        Debug.Log($"[FrogCartSetup] Сборка: {summary.result}, " +
                  $"размер {summary.totalSize / 1024 / 1024} МБ, ошибок {summary.totalErrors}");

        if (summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            throw new System.InvalidOperationException($"Сборка не удалась: {summary.result}");
    }

    static ColorPalette CreatePalette()
    {
        var palette = ScriptableObject.CreateInstance<ColorPalette>();

        palette.SetAll(new[]
        {
            // Первые пять — из 02-art.md, дословно. Дальше три добавленных: уровни
            // Food Hunt используют до десяти цветов, и без них картинку не показать.
            Entry("black",  "343941", "5A616C", "191C21"),
            Entry("red",    "EF4136", "FF8A7D", "A81F18"),
            Entry("orange", "FF9012", "FFC164", "BF6003"),
            Entry("yellow", "FFD52E", "FFF491", "CB9800"),
            Entry("blue",   "2E93E6", "8CCBFF", "125F9F"),
            Entry("green",  "5BC236", "A6EE7F", "2E7D18"),
            Entry("purple", "9B5BD6", "C9A3F0", "5E2E8C"),
            Entry("cream",  "F2E3C8", "FFF8EA", "C9AE86"),
            // Девятый цвет — предел GridModel.MaxColor: одна цифра на клетку
            // в строках уровня. Палитра обязана покрывать его целиком, иначе
            // уровень с большим числом красок теряет часть блоков и роняет
            // сборку на первом же обращении за недостающим цветом.
            Entry("teal",   "18B5A6", "76E4DA", "0B7A70"),
        });

        return Save(palette, $"{DataDir}/Palette.asset");
    }

    static ColorPalette.Entry Entry(string name, string baseHex, string lightHex, string darkHex)
        => new ColorPalette.Entry
        {
            name = name,
            baseColor = Hex(baseHex),
            light = Hex(lightHex),
            dark = Hex(darkHex),
        };

    static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out var color);
        return color;
    }

    static GameConfig CreateConfig()
        => Save(ScriptableObject.CreateInstance<GameConfig>(), $"{DataDir}/Config.asset");

    static LevelData.CartDef[] LoopCarts() => new[]
    {
        Cart(1, 5),
        Cart(3, 50),
        Cart(2, 28),
        Cart(4, 24),
        Cart(5, 2),
    };

    static LevelData.CartDef[] QueueNormal() => new[]
    {
        Cart(1, 8), Cart(2, 4), Cart(4, 6), Cart(1, 6), Cart(3, 8),
    };

    // 07-checklist.md: ёмкость чёрного 5+1+1 = 7 против 14 блоков — проигрыш сразу.
    static LevelData.CartDef[] QueueLoseTest() => new[]
    {
        Cart(1, 1), Cart(2, 4), Cart(4, 6), Cart(1, 1), Cart(3, 8),
    };

    static LevelData.CartDef Cart(int colorId, int capacity)
        => new LevelData.CartDef { colorId = colorId, capacity = capacity };

    static LevelData CreateLevel(string name, int number, LevelData.CartDef[] queue)
    {
        var level = ScriptableObject.CreateInstance<LevelData>();
        level.Fill(number, Level1Rows, LoopCarts(), queue);
        return Save(level, $"{DataDir}/{name}.asset");
    }

    static T Load<T>(string path) where T : Object
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null) throw new FileNotFoundException($"Ассет не найден: {path}");
        return asset;
    }

    static T Save<T>(T asset, string path) where T : Object
    {
        var existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing != null) AssetDatabase.DeleteAsset(path);

        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    static void BuildScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects,
                                                NewSceneMode.Single);

        // Загрузка строго ПОСЛЕ NewScene. Создание сцены выгружает ассеты, на которые
        // никто ещё не ссылается, и загруженные раньше объекты превращаются
        // в «уничтоженные»: проверка != null у них ложна, а в сцену пишется null.
        var palette = Load<ColorPalette>($"{DataDir}/Palette.asset");
        var config = Load<GameConfig>($"{DataDir}/Config.asset");
        var level = Load<LevelData>($"{DataDir}/Level01.asset");
        var loseTest = Load<LevelData>($"{DataDir}/Level01_LoseTest.asset");
        // Витринный уровень может быть ещё не сконвертирован — это не повод падать.
        var showcase = AssetDatabase.LoadAssetAtPath<LevelData>($"{DataDir}/Level0087.asset");

        var camera = Object.FindAnyObjectByType<Camera>();
        if (camera != null)
        {
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Hex("3A2412");
            camera.orthographic = true;
        }

        var go = new GameObject("GameBootstrap");
        var bootstrap = go.AddComponent<GameBootstrap>();
        go.AddComponent<AutoScreenshot>();

        bootstrap.EditorAssign(config, palette, level, loseTest, showcase);

        if (!bootstrap.EditorHasAllRefs)
            throw new System.InvalidOperationException(
                "Ссылки на ассеты не присвоились — сцену сохранять бессмысленно");

        EditorUtility.SetDirty(bootstrap);
        EditorSceneManager.MarkSceneDirty(scene);

        if (!EditorSceneManager.SaveScene(scene, ScenePath))
            throw new System.InvalidOperationException($"Не удалось сохранить сцену: {ScenePath}");

        var buildScenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        EditorBuildSettings.scenes = buildScenes;
    }
}
