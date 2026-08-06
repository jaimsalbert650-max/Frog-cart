using UnityEngine;
using UnityEngine.UI;

namespace FrogCart.Runtime
{
    /// <summary>
    /// Табличка отказа: всплывает над очередью, держится мгновение и тает.
    ///
    /// Плоская, как HUD и панели, и по той же причине: это обращение к игроку,
    /// а не предмет мира. Табличка в объёме легла бы на стол среди вагонеток и
    /// потерялась бы там, где её как раз надо прочитать сразу.
    ///
    /// Собственный отсчёт вместо твинера — из-за повторных тапов. Твины в Tweener
    /// складываются, отменить можно только все разом, и второй тап по занятому
    /// контуру гнал бы поверх первой анимации вторую: табличка мигала бы.
    /// Здесь повторный вызов просто отматывает время назад.
    /// </summary>
    public sealed class NoticeView : MonoBehaviour, INoticeView
    {
        /// <summary>Слова живут здесь, а не в логике: договор называет случай, не текст.</summary>
        const string NoRoomText = "NO SPACE";

        const float Appear = 0.14f;
        const float Fade = 0.3f;
        const float Life = 1.05f;

        const float PlateW = 158f;
        const float PlateH = 46f;

        /// <summary>
        /// Место таблички: по центру экрана, чуть выше переднего ряда очереди.
        ///
        /// Передний ряд стоит на 700, нижняя нитка контура идёт по 628 — табличка
        /// садится между ними. Ближе к очереди её и надо ставить: отказ относится
        /// к вагонетке, по которой только что ткнули.
        /// </summary>
        const float CenterX = 195f;
        const float CenterY = 645f;

        RectTransform _root;
        CanvasGroup _group;
        Text _text;

        float _elapsed = Life;

        /// <summary>Видна ли табличка прямо сейчас — для тестов.</summary>
        public bool Visible => _elapsed < Life;

        public void Build(RectTransform parent)
        {
            var go = new GameObject("Notice", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            go.transform.SetParent(parent, false);

            _root = (RectTransform)go.transform;
            SpecRect.PlaceCentered(_root, CenterX, CenterY, PlateW, PlateH);

            _group = go.GetComponent<CanvasGroup>();

            var plate = go.GetComponent<Image>();
            plate.raycastTarget = false;
            plate.sprite = ProcSprite.Make(new ProcSprite.Rounded
            {
                w = (int)PlateW, h = (int)PlateH,
                radii = new Vector4(16f, 16f, 16f, 16f),
                gradientAngleDeg = 180f,
                c0 = ProcSprite.Hex("FDF5E2"),
                c1 = ProcSprite.Hex("F7EACE"),
                c2 = ProcSprite.Hex("F0DFBA"),
                midStop = 0.5f,
                insetBottom = 5f,
                insetBottomColor = new Color(90f / 255f, 54f / 255f, 20f / 255f, 0.5f),
                key = "noticePlate",
            });

            // Красно-коричневый, а не общий коричневый HUD: отказ обязан отличаться
            // от подписей, которые висят на экране всегда.
            _text = UiText.Create("Text", _root, 22, ProcSprite.Hex("A33529"));

            Hide();
        }

        public void NoRoom()
        {
            if (_root == null) return;

            _text.text = NoRoomText;
            _elapsed = 0f;

            _root.gameObject.SetActive(true);
            Apply();
        }

        void Update()
        {
            if (_elapsed >= Life) return;

            // Время неигровое: табличка обязана дожить и в паузе, и на исходе,
            // где остальная сцена уже стоит.
            _elapsed += Time.unscaledDeltaTime;

            if (_elapsed >= Life) { Hide(); return; }

            Apply();
        }

        void Apply()
        {
            float scale = _elapsed < Appear
                ? Mathf.Lerp(0.7f, 1f, Tweener.Overshoot(_elapsed / Appear))
                : 1f;

            float fadeStart = Life - Fade;
            float alpha = _elapsed < fadeStart
                ? 1f
                : 1f - (_elapsed - fadeStart) / Fade;

            _root.localScale = Vector3.one * scale;
            _group.alpha = Mathf.Clamp01(alpha);
        }

        void Hide()
        {
            _elapsed = Life;
            _group.alpha = 0f;
            _root.gameObject.SetActive(false);
        }
    }
}
