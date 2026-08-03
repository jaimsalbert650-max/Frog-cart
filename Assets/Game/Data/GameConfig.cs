using UnityEngine;

namespace FrogCart.Data
{
    /// <summary>
    /// Все значения — из docs/unity-spec/04-gameplay.md и 05-feel-anim.md.
    /// Ни одно не придумано: если число меняется, оно меняется и в спеке.
    /// </summary>
    [CreateAssetMenu(menuName = "Frog Cart/Game Config", fileName = "Config")]
    public sealed class GameConfig : ScriptableObject
    {
        [Header("Движение")]
        [Range(0f, 60f)] public float railSpeed = 19f;

        [Header("Ввод")]
        [Range(0.04f, 0.22f)] public float chainDelay = 0.09f;

        [Header("Отдача")]
        [Range(1, 40)] public int shakeEvery = 10;
        public bool vibrate = true;

        [Header("Язык")]
        public float tongueOut = 0.20f;
        public float tongueBack = 0.13f;

        [Header("Затухания")]
        public float squashDecay = 9f;
        public float recoilDecay = 11f;
        public float recoilImpulse = 9f;

        [Header("Вагонетки")]
        public float cartExit = 0.34f;
        public float cartEnter = 0.30f;
        public float emptyToExit = 0.38f;
        public float exitToDock = 0.34f;
        public float queueShift = 0.30f;

        [Header("Исходы")]
        public float winDelay = 0.26f;
        public float loseDelay = 0.34f;
        public float startLoseCheckDelay = 0.40f;
        public float winFlash = 0.5f;
        public float winSilhouette = 1.2f;
        public float winPanelDelay = 1.1f;

        [Header("Прочее")]
        public float shakeDuration = 0.28f;
        public float wobbleDuration = 0.34f;
        public float numberPop = 0.22f;
        public float panelAppear = 0.30f;
        public float progressTween = 0.18f;
    }
}
