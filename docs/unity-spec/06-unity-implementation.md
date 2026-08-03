# 06. Реализация в Unity

Unity 2022.3 LTS+, 2D URP или встроенный рендер — не важно, всё на UI (Canvas).
Никаких сторонних пакетов не требуется; TextMeshPro — только для чисел HUD и вагонеток.

## Иерархия сцены
```
Canvas (Screen Space Overlay, Reference 390x844, Match 0)
└── Game (RectTransform 390x844, anchor top-left)
    ├── WoodBackground            (Image, доски)
    ├── RailLayer                 (8 UILineRenderer/Sprite-обводок по одному пути)
    ├── FrameOuter, FramePanel    (Image, кремовая рамка)
    ├── GridRoot (308x432 @ 41,-132)
    │   └── Cell_r_c ×224         (Image; префаб Cell)
    ├── FlashOverlay              (Image, белая вспышка по области рамки)
    ├── FrogLayer                 (Frog ×5, префаб Frog)
    ├── CartLayer                 (Cart ×5, префаб Cart)
    ├── TongueLayer               (Tongue ×5, префаб Tongue)
    ├── DockPanel                 (док + QueueCart ×5, префаб QueueCart)
    ├── HUD                       (LevelBadge, ProgressBar, PauseButton)
    ├── ConfettiLayer             (пул 46 частиц)
    └── PanelLayer                (WinPanel / LosePanel / PausePanel — одна панель, разные состояния)
```
Порядок в иерархии = порядок отрисовки: жабы **перед** вагонетками (корпус перекрывает
низ жабы, число всегда читаемо), язык — поверх всего игрового поля, HUD и панели — сверху.

## Префабы
| Префаб | Состав | Скрипт |
|---|---|---|
| `Cell` | 1 Image | — (управляется `GridView`) |
| `Cart` | тень, 2 колеса, корпус, табличка, TMP-число, цветная полоса | `CartView` |
| `Frog` | голова, 2 глаза + зрачки + блики, рот | `FrogView` |
| `Tongue` | линия (UILineRenderer), блик, кончик, блок | `TongueView` |
| `QueueCart` | как Cart, но крупнее и без вращения | `QueueCartView` |
| `Confetti` | 1 Image | `ConfettiParticle` |

Скруглённые прямоугольники: 9-slice спрайт «rounded» + отдельные Image для inset-бликов
и фасок (полоска сверху/снизу с alpha), тени — Image с blur-спрайтом или `Shadow`/`Outline`
эффектами UI. Кривая языка — `UILineRenderer` (генерация меша из 16 точек Безье).

## Классы C#
```
GameConfig        (ScriptableObject) : railSpeed, chainDelay, shakeEvery, тайминги
LevelData         (ScriptableObject) : string[] rows, CartDef[] loopCarts, CartDef[] queue
ColorPalette      (ScriptableObject) : PaletteEntry[5] { baseC, lightC, darkC }
LoopPath          (static/class)     : Sample(float s) → (Vector2 pos, float angleDeg); Perimeter
GameController    (MonoBehaviour)    : состояние, Eat(), CheckLose(), Win(), Restart(), Tick()
GridView          (MonoBehaviour)    : создание 224 ячеек, SetCell(r,c,colorId), Wobble(r,c), ShowGhost(bool)
GridInput         (MonoBehaviour)    : IPointerDown/Move/Up → HitTest → GameController.Eat
CartView          (MonoBehaviour)    : SetColor(), SetCount(), PopNumber(), place(pos, angle, scale, alpha)
FrogView          (MonoBehaviour)    : SetColor(), Place(x,y,scale), Squash(), Burp(), Clap(bool), MouthOpen(t)
TongueView        (MonoBehaviour)    : Fire(mouth, target, side, colorId), UpdateFrame(dt) → событие OnStick/OnDone
QueueView         (MonoBehaviour)    : отображение очереди + сдвиг
HudView           (MonoBehaviour)    : SetLevel(), SetProgress(pct), OnPause
PanelView         (MonoBehaviour)    : ShowWin/ShowLose/ShowPause/Hide
ScreenShake       (MonoBehaviour)    : Shake(0.28f)
ConfettiBurst     (MonoBehaviour)    : Play()
```
Вся логика — в `GameController`; `*View` только рисуют. Один `Update()` в
`GameController` двигает `dist`, обновляет слоты, жаб и языки (детерминированный порядок).

## Готовая математика контура
```csharp
public class LoopPath {
    const float RL = 15f, RR = 375f, RT = 68f, RB = 628f, RAD = 48f;
    struct Seg { public bool arc; public Vector2 a, b, c; public float a0, a1, len; }
    readonly List<Seg> segs = new List<Seg>();
    public float Perimeter { get; private set; }

    public LoopPath() {
        AddLine(new Vector2(RR - RAD, RT), new Vector2(RL + RAD, RT));
        AddArc(new Vector2(RL + RAD, RT + RAD), 270f, 180f);
        AddLine(new Vector2(RL, RT + RAD), new Vector2(RL, RB - RAD));
        AddArc(new Vector2(RL + RAD, RB - RAD), 180f, 90f);
        AddLine(new Vector2(RL + RAD, RB), new Vector2(RR - RAD, RB));
        AddArc(new Vector2(RR - RAD, RB - RAD), 90f, 0f);
        AddLine(new Vector2(RR, RB - RAD), new Vector2(RR, RT + RAD));
        AddArc(new Vector2(RR - RAD, RT + RAD), 0f, -90f);
        foreach (var s in segs) Perimeter += s.len;
    }
    void AddLine(Vector2 a, Vector2 b) {
        segs.Add(new Seg { arc = false, a = a, b = b, len = Vector2.Distance(a, b) });
    }
    void AddArc(Vector2 c, float a0, float a1) {
        segs.Add(new Seg { arc = true, c = c, a0 = a0, a1 = a1, len = RAD * Mathf.PI * 0.5f });
    }

    // s — путь по контуру; возвращает точку (spec-координаты, Y вниз) и угол в градусах
    public void Sample(float s, out Vector2 pos, out float angleDeg) {
        s = Mathf.Repeat(s, Perimeter);
        foreach (var g in segs) {
            if (s > g.len) { s -= g.len; continue; }
            float u = g.len > 0f ? s / g.len : 0f;
            if (!g.arc) {
                pos = Vector2.Lerp(g.a, g.b, u);
                Vector2 d = g.b - g.a;
                angleDeg = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            } else {
                float th = Mathf.Lerp(g.a0, g.a1, u) * Mathf.Deg2Rad;
                pos = g.c + new Vector2(Mathf.Cos(th), Mathf.Sin(th)) * RAD;
                angleDeg = Mathf.Atan2(-Mathf.Cos(th), Mathf.Sin(th)) * Mathf.Rad2Deg;
            }
            return;
        }
        pos = new Vector2(RL, RT); angleDeg = 180f;
    }
}
```
**Важно про знак угла.** Спецификация в экранной системе (Y вниз), а UI Unity — Y вверх.
Простейший путь: держать всю логику в spec-координатах и на выводе делать
`anchoredPosition = new Vector2(pos.x, -pos.y)`, `localEulerAngles = new Vector3(0,0,-angleDeg)`.
Тогда все формулы из этого документа переносятся 1:1.

## Позиционирование жабы (каждый кадр)
```csharp
float ar = angleDeg * Mathf.Deg2Rad;
float sa = Mathf.Abs(Mathf.Sin(ar)), ca = Mathf.Abs(Mathf.Cos(ar));
// центр корпуса вагонетки (локально (0, -18.5), lift — выезд/въезд наружу)
Vector2 bodyC = new Vector2(pos.x + (18.5f - lift) * Mathf.Sin(ar),
                            pos.y - (18.5f - lift) * Mathf.Cos(ar));
float halfY   = (44f * sa + 27f * ca) * 0.5f;     // пол-высота корпуса на экране
Vector2 frogBottom = new Vector2(bodyC.x, bodyC.y - halfY + 8f);  // перекрытие 8 px
// жаба (34x37, pivot низ-центр) ставится в frogBottom, БЕЗ поворота
Vector2 mouth = new Vector2(bodyC.x, frogBottom.y - 11f);         // откуда стартует язык
```

## Ввод (мышь + тач одним кодом)
На `GridRoot` — `Image` с `raycastTarget = true` и реализация
`IPointerDownHandler, IDragHandler, IPointerUpHandler`:
```csharp
RectTransformUtility.ScreenPointToLocalPointInRectangle(gridRect, e.position, e.pressEventCamera, out var lp);
int c = Mathf.FloorToInt(lp.x / 22f);
int r = Mathf.FloorToInt(-lp.y / 27f);   // pivot top-left
```
`IDragHandler` работает и для мыши, и для пальца; троттлинг цепочки — `chainDelay`.

## Замечания по производительности
- 224 ячейки — обычные Image; менять только `sprite`/`color`, не создавать/удалять.
- Пул: 5 языков, 46 конфетти, 224 ячейки — всё создаётся один раз.
- Обновление трансформов вагонеток/жаб/языков — вручную в `Update()`, без анимаций Animator.
- Коротких твинов много: сделать простой `Tweener` (или DOTween, если разрешён).
