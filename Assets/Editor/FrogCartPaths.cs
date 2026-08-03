using System.IO;
using UnityEditor;
using FrogCart.Data;

/// <summary>
/// Где в этом проекте лежит игра.
///
/// Редакторные скрипты живут в двух проектах сразу: в «Frog Cart» игра лежит прямо
/// в `Assets/Game`, в справочном «pixel Puzzles» она целиком убрана в
/// `Assets/_FrogCart`. Каждый жёстко прописанный путь верен ровно для одного из них
/// и молча ломает второй при синхронизации файла.
///
/// Эта мина уже сработала: `Build 3D Scene` падал с «Ассеты не найдены» при каждом
/// нажатии, сцена оставалась собранной до всех правок, и со стороны это выглядело
/// так, будто правки кода вообще не действуют. Разбирались долго.
///
/// Поэтому путь не задаётся, а ищется. Опорная точка — ассет конфига: он в проекте
/// один, и по его папке восстанавливается вся раскладка.
/// </summary>
public static class FrogCartPaths
{
    /// <summary>Папка с ассетами данных: палитра, конфиг, уровни.</summary>
    public static string DataDir()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:GameConfig"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (AssetDatabase.LoadAssetAtPath<GameConfig>(path) == null) continue;

            return Path.GetDirectoryName(path).Replace('\\', '/');
        }

        // Конфига ещё нет — значит проект собирают с нуля, и раскладка будет
        // такой, какую создаст FrogCartSetup.
        return "Assets/Game/Data";
    }

    /// <summary>Корень игры: папка, внутри которой лежат Game, Editor, Scenes.</summary>
    public static string Root()
        => Path.GetDirectoryName(Path.GetDirectoryName(DataDir())).Replace('\\', '/');

    /// <summary>
    /// Путь объёмной сцены. Если рядом уже лежит сцена с «3D» в имени — пишем в неё,
    /// а не заводим вторую: у пользователя открыта именно она, и новый файл рядом
    /// означал бы, что он продолжает смотреть на старую.
    /// </summary>
    public static string Scene3D()
    {
        string sceneDir = $"{Root()}/Scenes";

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
}
