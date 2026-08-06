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

    /// <summary>
    /// Палитра создаётся один раз и дальше живёт своей жизнью: её правит веб-редактор
    /// (tools/level-editor), и пересборка ассетов не имеет права затирать эти правки.
    /// Список ниже — первичное заполнение, а не источник истины. Чтобы вернуть
    /// заводские цвета, удалите Palette.asset и запустите пересборку снова.
    /// </summary>
    static ColorPalette CreatePalette()
    {
        var existing = AssetDatabase.LoadAssetAtPath<ColorPalette>($"{DataDir}/Palette.asset");
        if (existing != null)
        {
            Debug.Log("[FrogCartSetup] Palette.asset уже есть — оставлен как есть.");
            return existing;
        }

        var palette = ScriptableObject.CreateInstance<ColorPalette>();

        palette.SetAll(new[]
        {
            // Цвета сняты с самой игры, а не подобраны на глаз: пять кадров
            // оригинала (_reverse/reports/level_previews) дают 21 краску на
            // пятерых, из них девять частых взяты сюда. Прежние были из
            // 02-art.md — приглушённые black/red/orange поверх картинок, у
            // которых на деле кислотный пурпур и циан. Картинка от этого
            // читалась как чужая перекраска, чем и была.
            //
            // Слоты и порядок оставлены прежними: цвет в строках уровня — это
            // номер, и любая перестановка перекрасила бы все двадцать четыре
            // ассета разом. Заменён только девятый: бирюза ушла, розовый
            // пришёл — в оригинале его втрое больше, а бирюза почти не
            // встречается вне тёмной 00727B, которую от чёрного не отличить.
            //
            // Светлый и тёмный оттенки больше не подбираются каждый сам по
            // себе: light — 45% пути к белому, dark — 30% пути к чёрному. Это
            // не новое правило, а то же, что уже считает веб-редактор
            // (tools/level-editor, deriveShades): цвет, добавленный там, и
            // цвет, стоящий здесь, обязаны выглядеть одинаково.
            Entry("black",  "3D3844", "949298", "2B2730"),
            Entry("red",    "F94046", "FC9699", "AE2D31"),
            Entry("orange", "FF9A3A", "FFC793", "B26C29"),
            Entry("yellow", "FFE315", "FFF07E", "B29F0F"),
            Entry("blue",   "1889F9", "80BEFC", "1160AE"),
            Entry("green",  "00D94F", "73EA9E", "009837"),
            Entry("purple", "B945FC", "D899FD", "8230B0"),
            Entry("cream",  "F2CDA3", "F8E4CC", "A99072"),
            // Девятый цвет — предел GridModel.MaxColor: одна цифра на клетку
            // в строках уровня. Палитра обязана покрывать его целиком, иначе
            // уровень с большим числом красок теряет часть блоков и роняет
            // сборку на первом же обращении за недостающим цветом.
            Entry("pink",   "FF5DDE", "FFA6ED", "B2419B"),
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

    /// <summary>
    /// Как и палитра, конфиг создаётся один раз. Его крутят в вебе
    /// (tools/game-settings), и пересборка не должна сбрасывать настройку
    /// обратно к значениям полей GameConfig. Заводские числа вернутся, если
    /// удалить Config.asset и запустить пересборку снова.
    ///
    /// Числа по-прежнему живут в спеке: настроил в вебе — перенеси обратно
    /// в GameConfig.cs и docs/unity-spec кнопкой «Copy as C#».
    /// </summary>
    static GameConfig CreateConfig()
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameConfig>($"{DataDir}/Config.asset");
        if (existing != null)
        {
            Debug.Log("[FrogCartSetup] Config.asset уже есть — оставлен как есть.");
            return existing;
        }

        return Save(ScriptableObject.CreateInstance<GameConfig>(), $"{DataDir}/Config.asset");
    }

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
