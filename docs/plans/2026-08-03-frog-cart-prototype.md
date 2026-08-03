# Frog Cart Puzzle — план реализации прототипа

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** играбельный прототип по пакету `docs/unity-spec/` — картинка 14×16 из блоков, вагонетки с жабами на замкнутом рельсовом контуре, поедание блоков языком, очередь, победа, поражение, пауза.

**Architecture:** вся логика — в одном `GameController`, все `*View` только рисуют. Один `Update()` в контроллере детерминированно двигает `dist`, слоты, жаб и языки. Геометрия контура вынесена в `LoopPath` — чистый C# без Unity, поэтому тестируется отдельно. Расчёты ведутся в spec-координатах (начало — левый верхний угол, Y вниз), в Unity переводятся на выводе одной строкой.

**Tech Stack:** Unity 6000.5.3f1, uGUI (Canvas Screen Space Overlay, Reference 390×844, Match 0), TextMeshPro из `com.unity.ugui`. Сторонних пакетов нет. Твины — свой мини-`Tweener`, без Animator.

**Источник истины:** `docs/unity-spec/00-README.md` … `07-checklist.md`. Прототип-эталон: `docs/reference/Frog Cart Puzzle.dc.html` (открывается в браузере). Скриншоты: `docs/reference/screenshots/`.

---

## Правила работы с этим планом

1. **Числа не выдумывать.** Любая координата, размер, цвет и тайминг берутся из
   `unity-spec`. Если числа нет в спеке — оно берётся из HTML-прототипа, а не из головы.
2. **spec-координаты внутри, Unity-координаты на выводе.** Вся математика ведётся в
   системе «Y вниз, начало слева-сверху». На выводе:
   `anchoredPosition = new Vector2(pos.x, -pos.y)`, `localEulerAngles = new Vector3(0, 0, -angleDeg)`.
   Все RectTransform — `anchorMin = anchorMax = (0, 1)`.
3. **Сначала работает, потом красиво.** Задачи 1–8 дают играбельность на плейсхолдерах,
   9–14 — арт и ощущение. Не смешивать: иначе отладка механики утонет в градиентах.

---

## Структура файлов

```
Assets/
  Game/
    Core/                      FrogCart.Core.asmdef — чистый C#, без UnityEngine
      LoopPath.cs              геометрия контура: Sample(s) → позиция и угол
      LoseCheck.cs             остаток блоков против ёмкости по цветам
      GridModel.cs             int[16,14] + подсчёты по цветам
    Data/
      ColorPalette.cs          ScriptableObject: 5 цветов × base/light/dark
      LevelData.cs             ScriptableObject: string[] rows, CartDef[], очередь
      GameConfig.cs            ScriptableObject: railSpeed, chainDelay, shakeEvery, тайминги
    Runtime/
      GameController.cs        состояние, Eat, CheckLose, Win, Restart, Tick
      GridView.cs              224 ячейки, SetCell, Wobble, ShowGhost
      GridInput.cs             pointer → хит-тест → Eat
      CartView.cs              корпус, число, полоса, place
      FrogView.cs              голова, глаза, рот, Squash/Burp/Clap
      TongueView.cs            кривая Безье, Fire, OnStick, OnDone
      QueueView.cs             док и сдвиг очереди
      HudView.cs               уровень, прогресс, пауза
      PanelView.cs             Win / Lose / Pause
      ScreenShake.cs
      ConfettiBurst.cs
      Tweener.cs               мини-твинер: float, Vector2, ease-функции
      SpecRect.cs              хелпер: spec-координаты → RectTransform
  Tests/EditMode/
    FrogCart.Tests.asmdef
    LoopPathTests.cs
    LoseCheckTests.cs
    GridModelTests.cs
```

`LoopPath`, `LoseCheck`, `GridModel` — единственное, что тестируется автоматически: это
чистая арифметика. Остальное проверяется по чек-листу `07` глазами, и это честно —
писать тесты на «блик inset 0 2px» смысла нет.

---

## Task 1: Репозиторий и каркас проекта

**Files:**
- Create: `Assets/Game/Core/FrogCart.Core.asmdef`
- Create: `Assets/Tests/EditMode/FrogCart.Tests.asmdef`

- [ ] **Шаг 1: Проверить, что проект собирается**

```bash
"C:/Program Files/Unity/Hub/Editor/6000.5.3f1/Editor/Unity.exe" -batchmode -quit -nographics -projectPath "D:/Unity Projects/Frog Cart" -logFile -
```

Ожидается: exit code 0, в логе нет `error CS`.

- [ ] **Шаг 2: Первый коммит**

```bash
cd "D:/Unity Projects/Frog Cart"
git init
git add .gitignore Packages ProjectSettings docs
git commit -m "chore: проект Unity 6000.5.3f1 + uGUI, спека и эталонный прототип"
```

- [ ] **Шаг 3: Создать asmdef ядра**

`Assets/Game/Core/FrogCart.Core.asmdef`:

```json
{
    "name": "FrogCart.Core",
    "rootNamespace": "FrogCart.Core",
    "references": [],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": [],
    "autoReferenced": false,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": true
}
```

`noEngineReferences: true` — чтобы `LoopPath` и `LoseCheck` нельзя было случайно
завязать на сцену. Это единственная механическая гарантия тестируемости в проекте.

- [ ] **Шаг 4: Создать asmdef тестов**

`Assets/Tests/EditMode/FrogCart.Tests.asmdef`:

```json
{
    "name": "FrogCart.Tests",
    "rootNamespace": "FrogCart.Tests",
    "references": [ "FrogCart.Core", "UnityEngine.TestRunner", "UnityEditor.TestRunner" ],
    "includePlatforms": [ "Editor" ],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": [ "nunit.framework.dll" ],
    "autoReferenced": false,
    "defineConstraints": [ "UNITY_INCLUDE_TESTS" ],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **Шаг 5: Коммит**

```bash
cd "D:/Unity Projects/Frog Cart"
git add Assets
git commit -m "chore: сборки ядра и тестов"
```

---

## Task 2: Геометрия рельсового контура

Спека: `01-layout.md`, раздел «Замкнутый рельсовый контур»; готовый код — `06-unity-implementation.md`.

**Files:**
- Create: `Assets/Game/Core/LoopPath.cs`
- Test: `Assets/Tests/EditMode/LoopPathTests.cs`

- [ ] **Шаг 1: Написать падающий тест**

`Assets/Tests/EditMode/LoopPathTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;
using FrogCart.Core;

namespace FrogCart.Tests
{
    public class LoopPathTests
    {
        const float Tol = 0.01f;

        [Test]
        public void Perimeter_MatchesRoundedRectangleFormula()
        {
            var path = new LoopPath();

            // 2*(264 + 464) прямых + 4 дуги радиусом 48 = 1456 + 2*pi*48
            float expected = 1456f + 2f * Mathf.PI * 48f;

            Assert.AreEqual(expected, path.Perimeter, Tol);
        }

        [Test]
        public void StartOfPath_IsTopRightCornerGoingLeft()
        {
            var path = new LoopPath();
            path.Sample(0f, out var pos, out float angle);

            Assert.AreEqual(327f, pos.x, Tol);
            Assert.AreEqual(68f, pos.y, Tol);
            Assert.AreEqual(180f, Mathf.Abs(angle), Tol, "верхний сегмент едет влево");
        }

        [Test]
        public void SamplingWrapsAroundPerimeter()
        {
            var path = new LoopPath();
            path.Sample(12f, out var a, out float angleA);
            path.Sample(12f + path.Perimeter, out var b, out float angleB);

            Assert.AreEqual(a.x, b.x, Tol);
            Assert.AreEqual(a.y, b.y, Tol);
            Assert.AreEqual(angleA, angleB, Tol);
        }

        [Test]
        public void LeftSegment_GoesDownward()
        {
            var path = new LoopPath();
            // 264 прямая сверху + четверть дуги, дальше левый сегмент
            float s = 264f + 48f * Mathf.PI * 0.5f + 100f;
            path.Sample(s, out var pos, out float angle);

            Assert.AreEqual(15f, pos.x, Tol, "левый сегмент идёт по X = RL");
            Assert.AreEqual(216f, pos.y, Tol);
            Assert.AreEqual(90f, angle, Tol, "движение вниз по экрану");
        }

        [Test]
        public void InwardNormalPointsAtThePicture()
        {
            var path = new LoopPath();

            // Верхний сегмент: -Y локальной оси должен смотреть вниз, внутрь контура.
            path.Sample(30f, out _, out float top);
            Vector2 inwardTop = InwardNormal(top);
            Assert.Greater(inwardTop.y, 0.9f, "сверху внутрь — это вниз по экрану");

            // Левый сегмент: внутрь — вправо.
            path.Sample(264f + 48f * Mathf.PI * 0.5f + 100f, out _, out float left);
            Vector2 inwardLeft = InwardNormal(left);
            Assert.Greater(inwardLeft.x, 0.9f, "слева внутрь — это вправо по экрану");
        }

        static Vector2 InwardNormal(float angleDeg)
        {
            float a = angleDeg * Mathf.Deg2Rad;
            // локальная -Y в мировых spec-координатах
            return new Vector2(Mathf.Sin(a), -Mathf.Cos(a)) * -1f;
        }
    }
}
```

- [ ] **Шаг 2: Прогнать и убедиться, что падает**

```bash
"C:/Program Files/Unity/Hub/Editor/6000.5.3f1/Editor/Unity.exe" -batchmode -projectPath "D:/Unity Projects/Frog Cart" -runTests -testPlatform EditMode -testResults "D:/Unity Projects/Frog Cart/tmp/results.xml" -logFile -
```

Ожидается: `LoopPath` не найден.

- [ ] **Шаг 3: Написать LoopPath**

Взять код из `docs/unity-spec/06-unity-implementation.md`, раздел «Готовая математика контура», целиком, добавив `using UnityEngine;` для `Vector2`/`Mathf`.

Две правки к тому коду, обе обязательны:

```csharp
// 1. Границу сегмента брать по >=, иначе точка ровно на стыке уходит в следующий сегмент
//    с u = 1 вместо u = 0 — угол скачет.
if (s >= g.len) { s -= g.len; continue; }

// 2. Vector2/Mathf тянут UnityEngine, поэтому в asmdef ядра noEngineReferences остаётся
//    true только если заменить их на System.Numerics. Проще: перенести LoopPath.cs в
//    Assets/Game/Core и выставить noEngineReferences: false, оставив ссылку только на
//    UnityEngine.CoreModule. Тесты от этого не страдают.
```

Итог по второй правке: в `FrogCart.Core.asmdef` поставить

```json
    "noEngineReferences": false,
    "overrideReferences": true,
    "precompiledReferences": []
```

и не добавлять ничего кроме — ядро по-прежнему не увидит UI, сцены и MonoBehaviour-ов
по соглашению, а `Vector2` возьмёт из CoreModule.

- [ ] **Шаг 4: Прогнать тесты**

Ожидается: 5 тестов, все `Passed`.

Если `Perimeter_MatchesRoundedRectangleFormula` падает — сверить `RL/RR/RT/RB/RAD`
с `01-layout.md`. Значение «PERIM ≈ 1721.7» в спеке **неверно**: по её же числам
периметр равен **1757.6**. Расхождение безвредно, потому что код считает периметр сам,
но если где-то встретится литерал 1721.7 — заменить на `path.Perimeter`.

- [ ] **Шаг 5: Коммит**

```bash
cd "D:/Unity Projects/Frog Cart"
git add Assets
git commit -m "feat: геометрия замкнутого рельсового контура"
```

---

## Task 3: Модель сетки и проверка проигрыша

Спека: `03-level-data.md`, `04-gameplay.md` раздел «Проигрыш».

**Files:**
- Create: `Assets/Game/Core/GridModel.cs`
- Create: `Assets/Game/Core/LoseCheck.cs`
- Test: `Assets/Tests/EditMode/GridModelTests.cs`
- Test: `Assets/Tests/EditMode/LoseCheckTests.cs`

- [ ] **Шаг 1: Написать падающие тесты сетки**

`Assets/Tests/EditMode/GridModelTests.cs`:

```csharp
using NUnit.Framework;
using FrogCart.Core;

namespace FrogCart.Tests
{
    public class GridModelTests
    {
        static readonly string[] Balloon =
        {
            "00000055000000", "00000322300000", "00034322343000", "00234322343200",
            "00234322343200", "00234322343200", "00234322343200", "00034322343000",
            "00004322340000", "00000322300000", "00000022000000", "00000100100000",
            "00000111100000", "00000111100000", "00000111100000", "00000000000000",
        };

        [Test]
        public void ParsesRowsIntoGrid()
        {
            var grid = GridModel.FromRows(Balloon);

            Assert.AreEqual(16, grid.Rows);
            Assert.AreEqual(14, grid.Cols);
            Assert.AreEqual(5, grid.Get(0, 6), "синий флажок на верхушке");
            Assert.AreEqual(0, grid.Get(15, 0), "нижний ряд пустой");
            Assert.AreEqual(1, grid.Get(12, 5), "корзина");
        }

        [Test]
        public void CountsBlocksPerColorAsInSpec()
        {
            var grid = GridModel.FromRows(Balloon);

            Assert.AreEqual(14, grid.CountOfColor(1), "black");
            Assert.AreEqual(28, grid.CountOfColor(2), "red");
            Assert.AreEqual(30, grid.CountOfColor(3), "orange");
            Assert.AreEqual(14, grid.CountOfColor(4), "yellow");
            Assert.AreEqual(2, grid.CountOfColor(5), "blue");
            Assert.AreEqual(88, grid.TotalBlocks, "всего блоков на уровне 1");
        }

        [Test]
        public void SideColumnsAndLastRowAreEmpty()
        {
            var grid = GridModel.FromRows(Balloon);

            for (int r = 0; r < grid.Rows; r++)
            {
                Assert.AreEqual(0, grid.Get(r, 0));
                Assert.AreEqual(0, grid.Get(r, 1));
                Assert.AreEqual(0, grid.Get(r, 12));
                Assert.AreEqual(0, grid.Get(r, 13));
            }

            for (int c = 0; c < grid.Cols; c++)
                Assert.AreEqual(0, grid.Get(15, c));
        }

        [Test]
        public void ClearingCellDropsTheCount()
        {
            var grid = GridModel.FromRows(Balloon);
            grid.Clear(0, 6);

            Assert.AreEqual(0, grid.Get(0, 6));
            Assert.AreEqual(1, grid.CountOfColor(5));
            Assert.AreEqual(87, grid.TotalBlocks);
        }
    }
}
```

Числа в тесте — контрольные из `03-level-data.md`. Если они не сойдутся, значит строки
уровня переписаны с ошибкой, и это выяснится сейчас, а не при игре.

- [ ] **Шаг 2: Прогнать и убедиться, что падает**

Ожидается: `GridModel` не найден.

- [ ] **Шаг 3: Написать GridModel**

`Assets/Game/Core/GridModel.cs`:

```csharp
using System;

namespace FrogCart.Core
{
    /// <summary>Текущая картинка. Индексы: [ряд, столбец], 0 — пустая ячейка.</summary>
    public sealed class GridModel
    {
        public const int MaxColor = 5;

        readonly int[,] _cells;
        readonly int[] _perColor = new int[MaxColor + 1];

        public int Rows { get; }
        public int Cols { get; }
        public int TotalBlocks { get; private set; }

        GridModel(int rows, int cols)
        {
            Rows = rows;
            Cols = cols;
            _cells = new int[rows, cols];
        }

        public static GridModel FromRows(string[] rows)
        {
            if (rows == null || rows.Length == 0) throw new ArgumentException("Уровень пуст");

            int cols = rows[0].Length;
            var grid = new GridModel(rows.Length, cols);

            for (int r = 0; r < rows.Length; r++)
            {
                if (rows[r].Length != cols)
                    throw new ArgumentException($"Ряд {r}: длина {rows[r].Length}, ожидалось {cols}");

                for (int c = 0; c < cols; c++)
                {
                    int color = rows[r][c] - '0';
                    if (color < 0 || color > MaxColor)
                        throw new ArgumentException($"Ряд {r}, столбец {c}: недопустимый цвет '{rows[r][c]}'");

                    grid._cells[r, c] = color;
                    if (color == 0) continue;

                    grid._perColor[color]++;
                    grid.TotalBlocks++;
                }
            }

            return grid;
        }

        public int Get(int r, int c) => _cells[r, c];

        public bool InBounds(int r, int c) => r >= 0 && c >= 0 && r < Rows && c < Cols;

        public int CountOfColor(int color) => _perColor[color];

        public void Clear(int r, int c)
        {
            int color = _cells[r, c];
            if (color == 0) return;

            _cells[r, c] = 0;
            _perColor[color]--;
            TotalBlocks--;
        }
    }
}
```

Счётчики по цветам держатся инкрементально: проверка проигрыша вызывается после
каждого съеденного блока, а пересчитывать 224 ячейки на каждый тап незачем.

- [ ] **Шаг 4: Написать падающие тесты проигрыша**

`Assets/Tests/EditMode/LoseCheckTests.cs`:

```csharp
using NUnit.Framework;
using FrogCart.Core;

namespace FrogCart.Tests
{
    public class LoseCheckTests
    {
        static GridModel TwoBlacks() => GridModel.FromRows(new[] { "11", "00" });

        [Test]
        public void NotLost_WhenCapacityCoversRemaining()
        {
            var capacity = new int[GridModel.MaxColor + 1];
            capacity[1] = 2;

            Assert.IsFalse(LoseCheck.IsLost(TwoBlacks(), capacity, reservedPerColor: null));
        }

        [Test]
        public void Lost_WhenCapacityIsShort()
        {
            var capacity = new int[GridModel.MaxColor + 1];
            capacity[1] = 1;

            Assert.IsTrue(LoseCheck.IsLost(TwoBlacks(), capacity, reservedPerColor: null));
        }

        [Test]
        public void ReservedBlocksDoNotCountAsRemaining()
        {
            // Один блок уже «летит» на языке: он оплачен и из остатка вычитается.
            var capacity = new int[GridModel.MaxColor + 1];
            capacity[1] = 1;

            var reserved = new int[GridModel.MaxColor + 1];
            reserved[1] = 1;

            Assert.IsFalse(LoseCheck.IsLost(TwoBlacks(), capacity, reserved));
        }

        [Test]
        public void ChecksEveryColorIndependently()
        {
            var grid = GridModel.FromRows(new[] { "12", "00" });
            var capacity = new int[GridModel.MaxColor + 1];
            capacity[1] = 5;
            capacity[2] = 0;

            Assert.IsTrue(LoseCheck.IsLost(grid, capacity, null), "красному не хватает, хотя чёрного в избытке");
        }
    }
}
```

- [ ] **Шаг 5: Написать LoseCheck**

`Assets/Game/Core/LoseCheck.cs`:

```csharp
namespace FrogCart.Core
{
    /// <summary>
    /// Проигрыш по спеке 04: для любого цвета остаток блоков больше суммарной ёмкости
    /// вагонеток этого цвета — на контуре и в очереди вместе.
    /// </summary>
    public static class LoseCheck
    {
        /// <param name="capacityPerColor">сумма count живых вагонеток контура и всей очереди</param>
        /// <param name="reservedPerColor">блоки, по которым язык уже летит; null — если таких нет</param>
        public static bool IsLost(GridModel grid, int[] capacityPerColor, int[] reservedPerColor)
        {
            for (int color = 1; color <= GridModel.MaxColor; color++)
            {
                int remaining = grid.CountOfColor(color);
                if (reservedPerColor != null) remaining -= reservedPerColor[color];
                if (remaining <= 0) continue;

                if (remaining > capacityPerColor[color]) return true;
            }

            return false;
        }
    }
}
```

- [ ] **Шаг 6: Прогнать тесты**

Ожидается: 5 + 4 + 4 = 13 тестов, все `Passed`.

- [ ] **Шаг 7: Коммит**

```bash
cd "D:/Unity Projects/Frog Cart"
git add Assets
git commit -m "feat: модель сетки и проверка проигрыша по ёмкости цветов"
```

---

## Task 4: Данные — палитра, уровень, конфиг

Спека: `02-art.md` (палитра), `03-level-data.md` (уровень и вагонетки), `04-gameplay.md` (параметры).

**Files:**
- Create: `Assets/Game/Data/ColorPalette.cs`
- Create: `Assets/Game/Data/LevelData.cs`
- Create: `Assets/Game/Data/GameConfig.cs`
- Create: `Assets/Game/Data/Level01.asset`, `Palette.asset`, `Config.asset` (через меню Create)

- [ ] **Шаг 1: Написать ColorPalette**

`Assets/Game/Data/ColorPalette.cs`:

```csharp
using UnityEngine;

namespace FrogCart.Data
{
    [CreateAssetMenu(menuName = "Frog Cart/Color Palette")]
    public sealed class ColorPalette : ScriptableObject
    {
        [System.Serializable]
        public struct Entry
        {
            public string name;
            public Color baseColor;
            public Color light;
            public Color dark;
        }

        [SerializeField] Entry[] entries = new Entry[5];

        /// <summary>colorId 1..5; 0 — пустая ячейка, за палитрой не обращаться.</summary>
        public Entry Get(int colorId) => entries[colorId - 1];
    }
}
```

- [ ] **Шаг 2: Заполнить палитру из спеки**

Создать `Assets/Game/Data/Palette.asset` (меню Create → Frog Cart → Color Palette) и вбить
пять записей ровно как в `02-art.md`:

| id | name | base | light | dark |
|---|---|---|---|---|
| 1 | black | `#343941` | `#5A616C` | `#191C21` |
| 2 | red | `#EF4136` | `#FF8A7D` | `#A81F18` |
| 3 | orange | `#FF9012` | `#FFC164` | `#BF6003` |
| 4 | yellow | `#FFD52E` | `#FFF491` | `#CB9800` |
| 5 | blue | `#2E93E6` | `#8CCBFF` | `#125F9F` |

В инспекторе цвета вводить через поле Hex, alpha = 255.

- [ ] **Шаг 3: Написать LevelData**

`Assets/Game/Data/LevelData.cs`:

```csharp
using UnityEngine;

namespace FrogCart.Data
{
    [CreateAssetMenu(menuName = "Frog Cart/Level")]
    public sealed class LevelData : ScriptableObject
    {
        [System.Serializable]
        public struct CartDef
        {
            public int colorId;
            public int capacity;
        }

        [Tooltip("16 строк по 14 символов, цифра — индекс цвета, 0 — пусто")]
        [SerializeField] string[] rows;

        [Tooltip("Вагонетки на контуре, слоты 0..4")]
        [SerializeField] CartDef[] loopCarts = new CartDef[5];

        [Tooltip("Очередь в порядке подачи")]
        [SerializeField] CartDef[] queue;

        public string[] Rows => rows;
        public CartDef[] LoopCarts => loopCarts;
        public CartDef[] Queue => queue;
    }
}
```

- [ ] **Шаг 4: Создать уровень 1**

`Assets/Game/Data/Level01.asset`. Строки — из `03-level-data.md`, все 16, дословно.
Вагонетки контура: black 5, orange 50, red 28, yellow 24, blue 2.
Очередь: black 8, red 4, yellow 6, black 6, orange 8.

- [ ] **Шаг 5: Создать тестовый уровень проигрыша**

`Assets/Game/Data/Level01_LoseTest.asset` — копия `Level01`, но очередь `black 1 / black 1`
вместо `black 8 / black 6`. По `07-checklist.md` на нём панель проигрыша обязана появиться
через ~0.4 c после старта: ёмкость чёрного 7 против 14 блоков.

- [ ] **Шаг 6: Написать GameConfig**

`Assets/Game/Data/GameConfig.cs`:

```csharp
using UnityEngine;

namespace FrogCart.Data
{
    [CreateAssetMenu(menuName = "Frog Cart/Game Config")]
    public sealed class GameConfig : ScriptableObject
    {
        [Header("Движение")]
        [Range(0f, 60f)] public float railSpeed = 19f;

        [Header("Ввод")]
        [Range(0.04f, 0.22f)] public float chainDelay = 0.09f;

        [Header("Отдача")]
        [Range(1, 40)] public int shakeEvery = 10;

        [Header("Тайминги языка")]
        public float tongueOut = 0.20f;
        public float tongueBack = 0.13f;

        [Header("Тайминги вагонеток")]
        public float cartExit = 0.34f;
        public float cartEnter = 0.30f;
        public float replaceDelay = 0.34f;
        public float emptyToReplace = 0.38f;

        [Header("Исходы")]
        public float winDelay = 0.26f;
        public float loseDelay = 0.34f;
        public float startLoseCheckDelay = 0.40f;
    }
}
```

Значения — из таблиц `04-gameplay.md` и `05-feel-anim.md`. Ни одно не придумано.

- [ ] **Шаг 7: Коммит**

```bash
cd "D:/Unity Projects/Frog Cart"
git add Assets
git commit -m "feat: палитра, данные уровня и конфиг параметров"
```

---

## Task 5: Сцена и хелпер spec-координат

Спека: `01-layout.md`, `06-unity-implementation.md` раздел «Иерархия сцены».

**Files:**
- Create: `Assets/Scenes/Game.unity`
- Create: `Assets/Game/Runtime/SpecRect.cs`

- [ ] **Шаг 1: Написать SpecRect**

`Assets/Game/Runtime/SpecRect.cs`:

```csharp
using UnityEngine;

namespace FrogCart.Runtime
{
    /// <summary>
    /// Единственный мост между spec-координатами (начало слева-сверху, Y вниз) и uGUI.
    /// Вся математика игры ведётся в spec; сюда попадает только вывод.
    /// </summary>
    public static class SpecRect
    {
        public static void Anchor(RectTransform rt)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
        }

        public static void Place(RectTransform rt, float x, float y, float w, float h)
        {
            Anchor(rt);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, -y);
        }

        public static void MoveTo(RectTransform rt, Vector2 specPos)
            => rt.anchoredPosition = new Vector2(specPos.x, -specPos.y);

        public static void RotateTo(RectTransform rt, float specAngleDeg)
            => rt.localEulerAngles = new Vector3(0f, 0f, -specAngleDeg);

        /// <summary>Пивот в центре объекта — для вагонеток, жаб, конфетти.</summary>
        public static void CenterPivot(RectTransform rt)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
        }
    }
}
```

- [ ] **Шаг 2: Собрать сцену**

Создать `Assets/Scenes/Game.unity`. Иерархия — точно как в `06-unity-implementation.md`:

```
Canvas (Screen Space Overlay)
  CanvasScaler: Scale With Screen Size, Reference 390x844, Match = 0
└── Game (RectTransform 390x844, anchor top-left)
    ├── WoodBackground
    ├── RailLayer
    ├── FrameOuter, FramePanel
    ├── GridRoot          (308x432 @ x=41, y=132)
    ├── FlashOverlay
    ├── FrogLayer
    ├── CartLayer
    ├── TongueLayer
    ├── DockPanel
    ├── HUD
    ├── ConfettiLayer
    └── PanelLayer
```

Порядок в иерархии — это порядок отрисовки. Жабы **перед** вагонетками: корпус перекрывает
низ жабы, и число на табличке никогда не заслоняется.

На этом шаге всё — пустые `Image` с плоской заливкой, без градиентов. Арт в Task 10.

- [ ] **Шаг 3: Коммит**

```bash
cd "D:/Unity Projects/Frog Cart"
git add Assets
git commit -m "feat: сцена, canvas 390x844 и хелпер spec-координат"
```

---

## Task 6: Сетка на экране и ввод

Спека: `01-layout.md` разделы «Сетка блоков», `04-gameplay.md` раздел «Цепочка при удержании».

**Files:**
- Create: `Assets/Game/Runtime/GridView.cs`
- Create: `Assets/Game/Runtime/GridInput.cs`
- Create: `Assets/Game/Prefabs/Cell.prefab`

- [ ] **Шаг 1: Написать GridView**

`Assets/Game/Runtime/GridView.cs`:

```csharp
using UnityEngine;
using UnityEngine.UI;
using FrogCart.Data;

namespace FrogCart.Runtime
{
    public sealed class GridView : MonoBehaviour
    {
        public const int Cols = 14;
        public const int Rows = 16;
        public const float CW = 22f;
        public const float CH = 27f;
        public const float GX = 41f;
        public const float GY = 132f;

        [SerializeField] RectTransform gridRoot;
        [SerializeField] Image cellPrefab;
        [SerializeField] ColorPalette palette;
        [SerializeField] Sprite blockSprite;
        [SerializeField] Sprite emptySocketSprite;

        Image[,] _cells;

        public static Vector2 CellCenter(int r, int c)
            => new Vector2(GX + c * CW + CW * 0.5f, GY + r * CH + CH * 0.5f);

        public void Build()
        {
            _cells = new Image[Rows, Cols];

            for (int r = 0; r < Rows; r++)
            for (int c = 0; c < Cols; c++)
            {
                var img = Instantiate(cellPrefab, gridRoot);
                var rt = (RectTransform)img.transform;
                SpecRect.Place(rt, c * CW, r * CH, CW, CH);
                img.raycastTarget = false;
                _cells[r, c] = img;
            }
        }

        public void SetCell(int r, int c, int colorId)
        {
            var img = _cells[r, c];

            if (colorId == 0)
            {
                img.sprite = emptySocketSprite;
                img.color = new Color32(0xE0, 0xCA, 0xA1, 0xFF);
                return;
            }

            img.sprite = blockSprite;
            img.color = palette.Get(colorId).baseColor;
        }
    }
}
```

Ячейки создаются один раз и дальше только меняют спрайт и цвет — по замечанию о
производительности из `06`: 224 `Image`, ни одного `Instantiate` в игре.

- [ ] **Шаг 2: Написать GridInput**

`Assets/Game/Runtime/GridInput.cs`:

```csharp
using UnityEngine;
using UnityEngine.EventSystems;

namespace FrogCart.Runtime
{
    /// <summary>Мышь и палец обрабатываются одним кодом: IDragHandler покрывает оба.</summary>
    public sealed class GridInput : MonoBehaviour,
        IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] RectTransform gridRect;
        [SerializeField] GameController controller;

        float _lastChainTime;

        public void OnPointerDown(PointerEventData e)
        {
            if (!TryHit(e, out int r, out int c)) return;
            controller.Eat(r, c);
            _lastChainTime = Time.unscaledTime;
        }

        public void OnDrag(PointerEventData e)
        {
            if (Time.unscaledTime - _lastChainTime < controller.ChainDelay) return;
            if (!TryHit(e, out int r, out int c)) return;

            controller.Eat(r, c);
            _lastChainTime = Time.unscaledTime;
        }

        public void OnPointerUp(PointerEventData e) => _lastChainTime = 0f;

        bool TryHit(PointerEventData e, out int r, out int c)
        {
            r = c = -1;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    gridRect, e.position, e.pressEventCamera, out var lp))
                return false;

            c = Mathf.FloorToInt(lp.x / GridView.CW);
            r = Mathf.FloorToInt(-lp.y / GridView.CH);

            return r >= 0 && c >= 0 && r < GridView.Rows && c < GridView.Cols;
        }
    }
}
```

- [ ] **Шаг 3: Проверить руками**

Запустить сцену, тапнуть по блокам: в консоль из `GameController.Eat` пока пишется
`(r, c)`. Убедиться, что попадание совпадает с блоком под пальцем во всех четырёх углах
сетки, и что протяжка выдаёт цепочку, а не сплошной поток.

- [ ] **Шаг 4: Коммит**

```bash
cd "D:/Unity Projects/Frog Cart"
git add Assets
git commit -m "feat: отрисовка сетки и ввод с цепочкой по протяжке"
```

---

## Task 7: Вагонетки и жабы на контуре

Спека: `01-layout.md` «Размещение вагонеток», `02-art.md` «Вагонетка», «Жаба», `06` «Позиционирование жабы».

**Files:**
- Create: `Assets/Game/Runtime/CartView.cs`
- Create: `Assets/Game/Runtime/FrogView.cs`
- Create: `Assets/Game/Prefabs/Cart.prefab`, `Frog.prefab`

- [ ] **Шаг 1: Написать CartView**

`Assets/Game/Runtime/CartView.cs`:

```csharp
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using FrogCart.Data;

namespace FrogCart.Runtime
{
    public sealed class CartView : MonoBehaviour
    {
        [SerializeField] RectTransform root;
        [SerializeField] RectTransform body;
        [SerializeField] RectTransform plate;
        [SerializeField] TMP_Text countText;
        [SerializeField] Image colorStripe;
        [SerializeField] CanvasGroup group;

        public void Place(Vector2 specPos, float specAngle, float scale, float alpha)
        {
            SpecRect.MoveTo(root, specPos);
            SpecRect.RotateTo(root, specAngle);
            root.localScale = Vector3.one * scale;
            group.alpha = alpha;

            // Число всегда читается горизонтально — контр-вращаем табличку.
            plate.localEulerAngles = new Vector3(0f, 0f, specAngle);
        }

        public void SetColor(ColorPalette palette, int colorId)
        {
            var entry = palette.Get(colorId);
            colorStripe.color = entry.baseColor;
            countText.color = entry.dark;
        }

        public void SetCount(int count) => countText.text = count.ToString();
    }
}
```

- [ ] **Шаг 2: Написать FrogView с позиционированием из спеки**

`Assets/Game/Runtime/FrogView.cs`:

```csharp
using UnityEngine;

namespace FrogCart.Runtime
{
    public sealed class FrogView : MonoBehaviour
    {
        [SerializeField] RectTransform root;   // бокс 34x37, pivot низ-центр

        public Vector2 MouthSpecPos { get; private set; }

        /// <summary>
        /// Жаба НЕ дочерняя к повороту вагонетки: она всегда стоит вертикально,
        /// иначе на верхнем сегменте оказалась бы вверх ногами.
        /// Формулы — из 06-unity-implementation.md.
        /// </summary>
        public void PlaceOnRail(Vector2 railPos, float specAngleDeg, float lift)
        {
            float ar = specAngleDeg * Mathf.Deg2Rad;
            float sa = Mathf.Abs(Mathf.Sin(ar));
            float ca = Mathf.Abs(Mathf.Cos(ar));

            Vector2 bodyC = new Vector2(
                railPos.x + (18.5f - lift) * Mathf.Sin(ar),
                railPos.y - (18.5f - lift) * Mathf.Cos(ar));

            float halfY = (44f * sa + 27f * ca) * 0.5f;
            Vector2 bottom = new Vector2(bodyC.x, bodyC.y - halfY + 8f);

            SpecRect.MoveTo(root, bottom);
            root.localEulerAngles = Vector3.zero;

            MouthSpecPos = new Vector2(bodyC.x, bottom.y - 11f);
        }

        public void SetSquash(float squash)
        {
            root.localScale = new Vector3(1f + squash * 0.30f, 1f - squash * 0.26f, 1f);
        }
    }
}
```

- [ ] **Шаг 3: Расставить 5 слотов**

В `GameController` — размещение по спеке `01`:

```csharp
float slotOffset = i * loopPath.Perimeter / 5f + 60f;
float s = dist + slotOffset - recoil[i] + exitExtra[i];
loopPath.Sample(s, out var pos, out float angle);
```

`dist += config.railSpeed * Time.deltaTime` в `Update`, и только в состоянии `Play`.

- [ ] **Шаг 4: Проверить руками**

Запустить. Проверить по `07-checklist.md`:
- вагонетки едут **против часовой**: верх — влево, левый бок — вниз;
- корпус всегда ориентирован вдоль пути, а число читается горизонтально на всех сегментах;
- жаба стоит вертикально и на верхнем сегменте не перевёрнута;
- жабы верхних и боковых вагонеток не заслоняют заполненные блоки (столбцы 0–1 и 12–13
  и ряд 15 пустые именно для этого).

- [ ] **Шаг 5: Коммит**

```bash
cd "D:/Unity Projects/Frog Cart"
git add Assets
git commit -m "feat: вагонетки и жабы едут по контуру"
```

---

## Task 8: Язык и съедание блока

Спека: `04-gameplay.md` алгоритм `Eat`, `05-feel-anim.md` траектория языка.

**Files:**
- Create: `Assets/Game/Runtime/TongueView.cs`
- Modify: `Assets/Game/Runtime/GameController.cs`

- [ ] **Шаг 1: Написать TongueView**

`Assets/Game/Runtime/TongueView.cs`:

```csharp
using System;
using UnityEngine;

namespace FrogCart.Runtime
{
    public sealed class TongueView : MonoBehaviour
    {
        public event Action OnStick;   // язык долетел, блок пора убирать из сетки
        public event Action OnDone;    // язык вернулся, блок проглочен

        [SerializeField] UILineRenderer line;   // меш из 16 точек кривой
        [SerializeField] RectTransform tip;
        [SerializeField] RectTransform carried;

        Vector2 _target;
        float _side;
        float _t;
        bool _active;
        bool _stuck;
        float _outDuration = 0.20f;
        float _backDuration = 0.13f;

        public bool Active => _active;

        public void Fire(Vector2 target, float side, float outDuration, float backDuration)
        {
            _target = target;
            _side = side;
            _outDuration = outDuration;
            _backDuration = backDuration;
            _t = 0f;
            _active = true;
            _stuck = false;
            gameObject.SetActive(true);
        }

        /// <summary>mouth пересчитывается каждый кадр: вагонетка едет, язык тянется за ртом.</summary>
        public void UpdateFrame(float dt, Vector2 mouth)
        {
            if (!_active) return;

            _t += dt;
            float p;

            if (_t < _outDuration)
            {
                p = EaseOutBack(_t / _outDuration);
            }
            else
            {
                if (!_stuck) { _stuck = true; OnStick?.Invoke(); }

                float u = (_t - _outDuration) / _backDuration;
                if (u >= 1f)
                {
                    _active = false;
                    gameObject.SetActive(false);
                    OnDone?.Invoke();
                    return;
                }

                p = 1f - u * u;
            }

            Draw(mouth, p);
        }

        void Draw(Vector2 mouth, float p)
        {
            Vector2 d = _target - mouth;
            Vector2 perp = new Vector2(-d.y, d.x).normalized;
            Vector2 ctrl = (mouth + _target) * 0.5f + perp * d.magnitude * 0.17f * _side;

            Vector2 ctrl1 = Vector2.Lerp(mouth, ctrl, p);
            Vector2 tipPos = Quad(mouth, ctrl, _target, p);

            line.SetCurve(mouth, ctrl1, tipPos, thickness: 8f - 2f * p);
            SpecRect.MoveTo(tip, tipPos);

            if (_stuck)
            {
                carried.gameObject.SetActive(true);
                SpecRect.MoveTo(carried, tipPos);
                carried.localScale = Vector3.one * Mathf.Max(0.25f, p);
            }
        }

        public static float EaseOutBack(float x)
        {
            float k = x - 1f;
            return 1f + 2.1f * k * k * k + 1.05f * k * k;
        }

        static Vector2 Quad(Vector2 a, Vector2 ctrl, Vector2 b, float t)
        {
            float mt = 1f - t;
            return mt * mt * a + 2f * mt * t * ctrl + t * t * b;
        }
    }
}
```

`UILineRenderer` — свой компонент на `MaskableGraphic`, строящий меш из 16 точек кривой.
Готового в uGUI нет, писать придётся; это ~60 строк `OnPopulateMesh`.

- [ ] **Шаг 2: Написать Eat в GameController**

Алгоритм — дословно по `04-gameplay.md`:

```csharp
public void Eat(int r, int c)
{
    if (state != GameState.Play) return;

    int color = grid.Get(r, c);
    if (color == 0 || reserved.Contains((r, c))) return;

    int slot = FindNearestCart(color, GridView.CellCenter(r, c));
    if (slot < 0) { gridView.Wobble(r, c); return; }   // не промах, просто отказ

    reserved.Add((r, c));
    reservedPerColor[color]++;
    recoil[slot] = 9f;

    tongues[slot].Fire(GridView.CellCenter(r, c), SideFor(slot, r, c),
                       config.tongueOut, config.tongueBack);

    carts[slot].count--;
    cartViews[slot].PopNumber();
    eaten++;
    hud.SetProgress(eaten / (float)total);

    if (eaten % config.shakeEvery == 0) screenShake.Shake(0.28f);
}
```

Снятие блока происходит **по событию `OnStick`**, а не по таймеру:

```csharp
tongues[slot].OnStick += () =>
{
    grid.Clear(r, c);
    gridView.SetCell(r, c, 0);
    reserved.Remove((r, c));
    reservedPerColor[color]--;

    if (carts[slot].count == 0) StartCoroutine(ReplaceCart(slot));
    if (eaten >= total) StartCoroutine(WinAfter(config.winDelay));
    else CheckLose();
};
```

Выбор ближайшей вагонетки — минимум евклидовой дистанции от текущей позиции вагонетки
до центра ячейки, среди живых, нужного цвета, с `count > 0`, не в процессе выезда.
**Вагонетки в очереди не участвуют** — это отдельно оговорено в спеке.

- [ ] **Шаг 3: Проверить руками**

- тап по блоку: язык вылетает за 0.20 c с перелётом, прилипает, за 0.13 c возвращается с блоком;
- ячейка становится пустым гнездом, число на вагонетке уменьшается с «попом»;
- вагонетку отбрасывает назад по рельсу;
- каждый 10-й блок — тряска экрана;
- тап по цвету, для которого нет вагонетки на контуре: ячейка дрожит, ничего не съедено,
  прогресс не меняется.

- [ ] **Шаг 4: Коммит**

```bash
cd "D:/Unity Projects/Frog Cart"
git add Assets
git commit -m "feat: язык, съедание блока и отдача"
```

---

## Task 9: Очередь, замена вагонетки, победа и поражение

Спека: `04-gameplay.md` разделы «Замена», «Победа», «Проигрыш», «Пауза и рестарт».

**Files:**
- Create: `Assets/Game/Runtime/QueueView.cs`
- Create: `Assets/Game/Runtime/PanelView.cs`
- Modify: `Assets/Game/Runtime/GameController.cs`

- [ ] **Шаг 1: Замена опустевшей вагонетки**

Последовательность строго по спеке:
1. `exitT: 0 → 1` за 0.34 c — вагонетка ускоряется по рельсу (+90 px по дуге), масштаб → 0.5,
   уезжает наружу на 30 px, alpha → 0. Перед этим жаба «рыгает» — поп-масштаб 0.3 c.
2. Через 340 мс слот берёт первую вагонетку из очереди; очередь едет влево 0.3 c с ease back;
   новая появляется `enterT: 1 → 0` за 0.3 c (scale 0.55 → 1, подъём на 46 px снизу, fade).
3. Очередь пуста → `live = false`, слот скрыт.
4. После замены — `CheckLose()`.

- [ ] **Шаг 2: Победа**

1. Вспышка по области картинки: белый, 0.5 c, alpha 0 → .85 → 0.
2. Силуэт: 1.2 c показать исходную картинку плоскими блоками (цвет `base`, без градиента,
   внутренняя обводка `rgba(255,255,255,.35)`).
3. Все жабы аплодируют: `clap = sin(time*11)*0.12`, поворот `clap*55°`.
4. Конфетти — 46 частиц по параметрам из `02-art.md`.
5. Через 1.1 c — панель победы: три золотые звезды, довольная жаба, кнопка Retry.

- [ ] **Шаг 3: Поражение**

`CheckLose()` вызывается: после каждого съеденного блока, после каждой замены вагонетки,
и один раз через `startLoseCheckDelay` (0.40 c) после старта уровня.

```csharp
void CheckLose()
{
    for (int color = 1; color <= GridModel.MaxColor; color++)
        capacity[color] = 0;

    foreach (var cart in carts)
        if (cart.live) capacity[cart.colorId] += cart.count;

    for (int i = queueIndex; i < queue.Length; i++)
        capacity[queue[i].colorId] += queue[i].capacity;

    if (!LoseCheck.IsLost(grid, capacity, reservedPerColor)) return;

    foreach (var t in tongues) t.Stop();
    state = GameState.Lose;
    StartCoroutine(ShowLoseAfter(config.loseDelay));
}
```

- [ ] **Шаг 4: Пауза и рестарт**

`Retry` полностью пересоздаёт уровень: сетка, вагонетки, очередь, `dist = 0`, все таймеры
и языки сброшены, слоты снова видимы, прогресс 0 %. Проще всего — один метод `BuildLevel()`,
вызываемый и на старте, и на Retry, без частичного сброса.

- [ ] **Шаг 5: Проверить механику проигрыша тестовым уровнем**

Подставить `Level01_LoseTest.asset` из Task 4. Панель проигрыша обязана появиться примерно
через 0.4 c после старта, без единого тапа.

- [ ] **Шаг 6: Коммит**

```bash
cd "D:/Unity Projects/Frog Cart"
git add Assets
git commit -m "feat: очередь, замена вагонеток, победа и поражение"
```

---

## Task 10: Арт — дерево, рамка, блоки, гнёзда

Спека: `02-art.md`.

- [ ] **Шаг 1: Фон и рамка**

Доски `#8a5a30` с полосами `#8f5e33` (54 px) / `#7a4d28` (4) / `#6d4322` (2), поверх
вертикальная текстура `rgba(0,0,0,.05)` шириной 2 px с шагом 11. Внутренняя виньетка по
краям экрана. Кремовая рамка: внешняя X 29 Y 92 W 332 H 512 R 26, внутренняя вдавленная
X 37 Y 100 W 316 H 496 R 20.

- [ ] **Шаг 2: Блок и пустое гнездо**

Блок 20×25 со смещением +1/+1, радиус 7, градиент 168° light→base(46%)→dark, верхний блик,
нижняя фаска, контактная тень. Гнездо 16×19 со смещением +3/+4, радиус 5, `#e0caa1`,
вдавленность.

- [ ] **Шаг 3: Рельсы**

Восемь слоёв по одной осевой, сверху вниз по списку из `01-layout.md`: тень под шпалами,
тёмная доска, светлая доска, шпалы пунктиром 5/15, внутренний рельс (подложка + металл),
внешний рельс (подложка + металл). Внутренний контур вписан на 8 px внутрь, внешний — на 8 наружу.

- [ ] **Шаг 4: Сверить со скриншотом**

Открыть `docs/reference/screenshots/full.png` рядом с игрой. Различия в пропорциях
и цвете — повод править числа, а не «на глаз похоже».

- [ ] **Шаг 5: Коммит**

```bash
cd "D:/Unity Projects/Frog Cart"
git add Assets
git commit -m "feat: арт поля — дерево, рамка, блоки, рельсы"
```

---

## Task 11: Арт — вагонетка, жаба, язык, очередь

Спека: `02-art.md` разделы «Вагонетка», «Жаба», «Язык», «Мини-вагонетка очереди».

- [ ] **Шаг 1: Вагонетка**

Контактная тень 42×10, два колеса 12×12 (X -17 и +5, Y -11), корпус 44×27 с радиусами
6/6/10/10 и градиентом `#c68f52 → #9c6231(55%) → #6f4321`, табличка 26×18 с кольцом 2.5 px
цветом вагонетки, цветная полоса 36×4 снизу, число 14 px weight 800 цветом `dark`.

- [ ] **Шаг 2: Жаба**

Бокс 34×37, pivot низ-центр. Голова 30×27 с радиусами 15/15/12/12; два глаза 17×17 с
зрачками 8×8 и бликами 6×5; рот 15×9 с радиусами 2/2/10/10, градиент `#5c2130 → #8d3346`.

- [ ] **Шаг 3: Язык**

Линия `#e04f6d` толщиной 8→6 с round cap, блик `#ff93aa` толщиной 3, кончик r 5.5 `#f2657f`,
утаскиваемый блок 20×25 радиус 6 цветом блока, масштаб `max(0.25, p)`.

- [ ] **Шаг 4: Док и очередь**

Панель X 0 Y 648 W 390 H 196, верхние углы 26. Рельс дока: планка Y 798 H 9 + металл
Y 802 X 14 W 362 H 4. Мини-вагонетки: Y 704, 78×96, шаг `x = 8 + i*88` — пятая намеренно
уезжает за правый край.

- [ ] **Шаг 5: Сверить с `cart-zoom.png` и `top.png`**

- [ ] **Шаг 6: Коммит**

```bash
cd "D:/Unity Projects/Frog Cart"
git add Assets
git commit -m "feat: арт вагонеток, жаб, языка и очереди"
```

---

## Task 12: HUD и панели

Спека: `02-art.md` разделы «HUD», «Панели».

- [ ] **Шаг 1: HUD**

Полоса высотой 60 с градиентом сверху. Бейдж уровня 38×38 R 12. Прогресс-бар высотой 22
R 11, заливка `#8fe05a → #4fae2c`, tween 0.18 c ease-out, по центру процент 12 px weight 800.
Кнопка паузы 38×38 с двумя полосками 5×16, при нажатии сдвиг +2 px.

- [ ] **Шаг 2: Панели**

Одна панель, три состояния. Затемнение `rgba(35,18,4,.62)`. Панель W 290 R 30, появление
scale 0.7 → 1.04 → 1 за 0.30 c ease back. Три звезды 44/58/44 — золотые на победе, `#cbb78f`
на поражении. Герой-жаба 96×86 с «дыханием» ±4 px за 1.6 c. Кнопки круглые 64×64.

**В панелях нет текста — только иконки.** Это отдельный пункт чек-листа.

- [ ] **Шаг 3: Коммит**

```bash
cd "D:/Unity Projects/Frog Cart"
git add Assets
git commit -m "feat: HUD и панели победы, поражения, паузы"
```

---

## Task 13: Ощущение — тайминги и отдача

Спека: `05-feel-anim.md`.

- [ ] **Шаг 1: Свести все тайминги в GameConfig и выверить**

| Событие | Длительность | Кривая |
|---|---|---|
| Язык наружу | 0.20 c | easeOutBack до ≈1.05 |
| Язык обратно | 0.13 c | `p = 1 - u²` |
| Squash жабы | ~0.12 c затухания | экспонента, коэф. 9/с |
| Recoil вагонетки | ~0.1 c затухания | экспонента, коэф. 11/с |
| «Поп» числа | 0.22 c | 1 → 1.42 → 1, ease back |
| Тряска экрана | 0.28 c | (-4,2), (3,-3), (-2,-1) |
| Дрожание ячейки | 0.34 c | ±3 px и ±5° |
| Выезд вагонетки | 0.34 c | ease-in по дуге |
| Въезд вагонетки | 0.30 c | scale 0.55→1 |
| Сдвиг очереди | 0.30 c | cubic-bezier(.3,1.4,.5,1) |
| Появление панели | 0.30 c | 0.7 → 1.04 → 1 |
| Прогресс-бар | 0.18 c | ease-out |

- [ ] **Шаг 2: Проверить полный набор отдачи на один тап**

По `05-feel-anim.md`, на каждое съедание обязаны сработать все шесть: блок выдёргивается
со щелчком, вагонетку отбрасывает, число подпрыгивает, жаба приседает и открывает рот,
прогресс дёргается вперёд, каждый 10-й — тряска.

- [ ] **Шаг 3: Вибрация**

`Handheld.Vibrate()` на каждый 10-й блок, только на Android/iOS, за флагом в конфиге.

- [ ] **Шаг 4: Коммит**

```bash
cd "D:/Unity Projects/Frog Cart"
git add Assets
git commit -m "feat: тайминги и тактильная отдача"
```

---

## Task 14: Приёмка по чек-листу

**Files:** нет — это приёмка.

- [ ] **Шаг 1: Прогнать автотесты**

```bash
"C:/Program Files/Unity/Hub/Editor/6000.5.3f1/Editor/Unity.exe" -batchmode -projectPath "D:/Unity Projects/Frog Cart" -runTests -testPlatform EditMode -testResults "D:/Unity Projects/Frog Cart/tmp/results.xml" -logFile -
```

Ожидается: 13 тестов, `failed="0"`.

- [ ] **Шаг 2: Пройти чек-лист `07-checklist.md` целиком**

Все 30 пунктов: раскладка, механика, тест проигрыша, арт и ощущение. Пункт, который не
выполняется, — это не «мелочь на потом», а незакрытая задача.

- [ ] **Шаг 3: Проверить на трёх пропорциях**

16:9, 19.5:9, 20:9 — ничего не обрезается, тап-таргеты кнопок ≥ 44 px.

- [ ] **Шаг 4: Сверить с эталоном**

Открыть `docs/reference/Frog Cart Puzzle.dc.html` в браузере и сыграть рядом. Расхождения
в ощущении — повод сверить тайминги, а не «и так нормально».

- [ ] **Шаг 5: Финальный коммит**

```bash
cd "D:/Unity Projects/Frog Cart"
git add -A
git commit -m "chore: прототип принят по чек-листу 07"
```

---

## Чего в этом плане нет

- **Уровней, кроме первого и тестового.** Пайплайн уровней (импорт PNG → строки) —
  следующий этап, после того как один уровень заиграет.
- **Меты:** прогресс, монеты, жизни, магазин. Прототип заканчивается на Retry.
- **Звука.** В спеке его нет ни строчкой.
- **Тестов на арт.** Проверка блика и градиента — глазами по чек-листу; автотест здесь
  дороже и бесполезнее ручной сверки со скриншотом.
