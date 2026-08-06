using NUnit.Framework;
using FrogCart.Runtime;

namespace FrogCart.Tests
{
    /// <summary>
    /// Ключ `-uselevel` и разбор имени уровня.
    ///
    /// Номер уровня набирают с руки, поэтому `Level0283`, `0283` и `283` обязаны
    /// открывать одно и то же. Проверяется тестом, а не запуском плеера: правило
    /// целиком в разборе строки, а сборка ради него — четыре минуты.
    /// </summary>
    public class LevelOverrideTests
    {
        [Test]
        public void FullAssetNameMatches()
        {
            Assert.IsTrue(Game3DBootstrap.SameLevel("Level0283", "Level0283"));
        }

        [Test]
        public void CaseDoesNotMatter()
        {
            Assert.IsTrue(Game3DBootstrap.SameLevel("Level0283", "level0283"));
            Assert.IsTrue(Game3DBootstrap.SameLevel("Level0283", "LEVEL0283"));
        }

        [Test]
        public void BareNumberMatchesWithAndWithoutLeadingZeros()
        {
            Assert.IsTrue(Game3DBootstrap.SameLevel("Level0283", "0283"));
            Assert.IsTrue(Game3DBootstrap.SameLevel("Level0283", "283"));
            Assert.IsTrue(Game3DBootstrap.SameLevel("Level0004", "4"));
        }

        [Test]
        public void DifferentLevelsDoNotMatch()
        {
            Assert.IsFalse(Game3DBootstrap.SameLevel("Level0283", "282"));
            Assert.IsFalse(Game3DBootstrap.SameLevel("Level0004", "40"));
        }

        /// <summary>
        /// Хвост после цифр значим. Без этого Level01_LoseTest — уровень, на
        /// котором нарочно проигрывают, — совпал бы с обычным Level01, и проверка
        /// проигрыша молча шла бы не на том уровне.
        /// </summary>
        [Test]
        public void SuffixAfterTheNumberIsNotIgnored()
        {
            Assert.IsFalse(Game3DBootstrap.SameLevel("Level01_LoseTest", "01"));
            Assert.IsFalse(Game3DBootstrap.SameLevel("Level01_LoseTest", "Level01"));
            Assert.IsFalse(Game3DBootstrap.SameLevel("Level01", "Level01_LoseTest"));

            // По полному имени он по-прежнему находится.
            Assert.IsTrue(Game3DBootstrap.SameLevel("Level01_LoseTest", "Level01_LoseTest"));
        }

        [Test]
        public void EmptyAndNonsenseDoNotMatchAnything()
        {
            Assert.IsFalse(Game3DBootstrap.SameLevel("Level0283", ""));
            Assert.IsFalse(Game3DBootstrap.SameLevel("Level0283", "showcase"));
        }
    }
}
