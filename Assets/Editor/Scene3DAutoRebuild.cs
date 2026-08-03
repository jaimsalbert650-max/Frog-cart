using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Пересборка объёмной сцены сама, когда код изменился.
///
/// Сцена не хранит ничего своего: всё, что в ней есть, — объект `Game3DBootstrap`
/// со ссылками на ассеты, а вся геометрия строится в `Awake`. Поэтому «пересобрать
/// сцену» — это не творческая работа, а обязательный шаг после каждой правки
/// `*3D*.cs`, и держать его на ручном нажатии было ошибкой: за сессию из-за него
/// несколько раз получалось, что код новый, а на экране старая картинка, и время
/// уходило на выяснение, почему «правки не действуют».
///
/// Срабатывает после перекомпиляции скриптов и только когда это безопасно:
/// не в Play mode, только если открыта именно объёмная сцена и в ней нет
/// несохранённых изменений.
/// </summary>
[InitializeOnLoad]
static class Scene3DAutoRebuild
{
    const string MenuPath = "Frog Cart/Auto Rebuild 3D Scene";
    const string PrefKey = "FrogCart.AutoRebuild3D";

    static bool Enabled
    {
        get => EditorPrefs.GetBool(PrefKey, true);
        set => EditorPrefs.SetBool(PrefKey, value);
    }

    static Scene3DAutoRebuild()
    {
        // delayCall, а не прямой вызов: на момент статического конструктора
        // AssetDatabase ещё не готов отвечать на запросы.
        EditorApplication.delayCall += CheckAndRebuild;
    }

    [MenuItem(MenuPath)]
    static void Toggle() => Enabled = !Enabled;

    [MenuItem(MenuPath, isValidateFunction: true)]
    static bool ToggleValidate()
    {
        Menu.SetChecked(MenuPath, Enabled);
        return true;
    }

    static void CheckAndRebuild()
    {
        if (!Enabled) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        string scenePath = FrogCartPaths.Scene3D();
        if (!File.Exists(scenePath)) return;   // сцены ещё нет — собирать нечего

        var active = EditorSceneManager.GetActiveScene();

        // Открыта другая сцена — не вырываем её из-под рук.
        if (active.path != scenePath) return;

        // Несохранённые изменения дороже автоматизации: сборка стирает сцену
        // целиком, поэтому в таком случае только предупреждаем.
        if (active.isDirty && IsStale(scenePath))
        {
            Debug.LogWarning("[Scene3D] Сцена устарела относительно кода, но в ней есть "
                           + "несохранённые изменения. Пересоберите вручную: "
                           + "Frog Cart → Build 3D Scene.");
            return;
        }

        RebuildIfStale();
    }

    /// <summary>
    /// Пересобрать сцену, если код новее её. Без проверок открытой сцены и Play mode —
    /// они в <see cref="CheckAndRebuild"/>.
    ///
    /// Вынесено отдельно и сделано публичным, чтобы эту часть можно было прогнать
    /// из batchmode: в нём открыта пустая сцена, и автоматический путь до сюда
    /// просто не доходит, а проверять надо именно её.
    /// </summary>
    public static bool RebuildIfStale()
    {
        string scenePath = FrogCartPaths.Scene3D();

        if (File.Exists(scenePath) && !IsStale(scenePath))
        {
            Debug.Log("[Scene3D] Сцена свежая, пересборка не нужна.");
            return false;
        }

        Scene3DSetup.BuildScene();
        Debug.Log("[Scene3D] Сцена пересобрана: код новее сцены.");
        return true;
    }

    /// <summary>Есть ли скрипт игры новее сохранённой сцены.</summary>
    static bool IsStale(string scenePath)
    {
        var sceneTime = File.GetLastWriteTimeUtc(scenePath);
        string root = FrogCartPaths.Root();

        foreach (string folder in new[] { $"{root}/Game", $"{root}/Editor" })
        {
            if (!Directory.Exists(folder)) continue;

            foreach (string file in Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories))
                if (File.GetLastWriteTimeUtc(file) > sceneTime) return true;
        }

        // Ассеты данных тоже считаются: смена уровня меняет ссылку внутри сцены.
        string dataDir = FrogCartPaths.DataDir();
        if (Directory.Exists(dataDir))
        {
            foreach (string file in Directory.EnumerateFiles(dataDir, "*.asset"))
                if (File.GetLastWriteTimeUtc(file) > sceneTime) return true;
        }

        return false;
    }
}
