using UnityEngine;
using FrogCart.Data;

namespace FrogCart.Runtime
{
    /// <summary>
    /// Договоры представлений. Логика игры о способе отрисовки знать не должна —
    /// иначе вторую отрисовку к ней не подключить.
    ///
    /// Координаты во всех договорах — spec-координаты (начало слева-сверху, Y вниз),
    /// те же, что в docs/unity-spec. Плоская отрисовка кладёт их на Canvas, объёмная
    /// разворачивает в плоскость XZ. Вся математика — LoopPath, центры клеток, точки
    /// выхода языка — остаётся одна на обе.
    /// </summary>
    public interface IGridView
    {
        int Rows { get; }
        int Cols { get; }

        Vector2 CellCenter(int r, int c);

        void SetCell(int r, int c, int colorId);

        /// <summary>Прочность клетки: 1 — обычная, больше — её надо пробивать.</summary>
        void SetCellArmour(int r, int c, int hp);

        /// <summary>Скрытая клетка показывает не свой цвет, а заглушку.</summary>
        void SetCellHidden(int r, int c, bool hidden);

        void Wobble(int r, int c, float duration);
        void ShowSilhouette(string[] rows, bool visible);
    }

    public interface ICartView
    {
        /// <param name="railPos">точка на контуре в spec-координатах</param>
        /// <param name="railAngle">угол касательной в градусах, экранная система</param>
        void Place(Vector2 railPos, float railAngle, float scale, float alpha);

        void SetColor(ColorPalette palette, int colorId);
        void SetCount(int count);

        /// <summary>Сколько льда осталось сколоть. Ноль — вагонетка работает.</summary>
        void SetFrozen(int count);

        /// <summary>Вагонетка в связке: уедет вместе с напарницей.</summary>
        void SetLinked(bool linked);

        void PopNumber(float duration);
        void SetVisible(bool visible);
    }

    public interface IFrogView
    {
        /// <summary>Точка, откуда вылетает язык. В spec-координатах.</summary>
        Vector2 MouthSpecPos { get; }

        void PlaceOnRail(Vector2 railPos, float railAngleDeg, float lift, float scale);
        void SetColor(ColorPalette palette, int colorId);
        void SetSquash(float value);
        void SetClap(float value);
        void SetAlpha(float value);
        void SetGaze(float offsetY);
        void SetVisible(bool visible);
    }

    public interface ITongueView
    {
        bool Active { get; }

        void Fire(Vector2 target, float side, float outDuration, float backDuration);
        void SetCarriedBlock(ColorPalette palette, int colorId);
        void UpdateFrame(float dt, Vector2 mouth);
        void Stop();
    }

    public interface IHudView
    {
        void SetLevel(int level);
        void SetProgress(float value, bool instant);
    }

    public interface IPanelView
    {
        void ShowWin();
        void ShowLose();
        void ShowPause();
        void Hide();
    }

    public interface IQueueView
    {
        void Rebuild(System.Collections.Generic.List<LevelData.CartDef> queue, int startIndex);
        void Shift(System.Collections.Generic.List<LevelData.CartDef> queue, int startIndex, float duration);
    }

    public interface IFlashOverlay
    {
        void SetAlpha(float alpha);
    }

    /// <summary>Вспышка победы на плоской отрисовке — обычный Image на Canvas.</summary>
    public sealed class ImageFlash : IFlashOverlay
    {
        readonly UnityEngine.UI.Image _image;

        public ImageFlash(UnityEngine.UI.Image image) => _image = image;

        public void SetAlpha(float alpha)
            => _image.color = new Color(1f, 1f, 1f, alpha);
    }
}
