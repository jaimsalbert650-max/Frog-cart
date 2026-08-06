using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using FrogCart.Runtime;

namespace FrogCart.Tests
{
    /// <summary>
    /// В очереди стоят те же жабы, что на контуре.
    ///
    /// Раньше очередь лепила голову сама: шар с приделанными глазами и усиками.
    /// Пока на контуре стоял такой же процедурный персонаж, разницы не было, но с
    /// приходом вылепленной головы очередь и рельс стали показывать двух разных
    /// существ. Проверяется поэтому не картинка, а источник геометрии: голова в
    /// очереди обязана браться из того же ассета.
    /// </summary>
    public class QueueHeadTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            SceneManager.LoadScene("Game3D", LoadSceneMode.Single);
            yield return null;
            yield return null;
        }

        static Transform QueueHead()
        {
            var queue = Object.FindAnyObjectByType<Queue3DView>();
            Assert.IsNotNull(queue, "объёмная очередь не построена");

            var root = GameObject.Find("Queue3D");
            Assert.IsNotNull(root, "корня очереди нет в сцене");

            var head = root.transform.Find("QueueCart_0/Head");
            Assert.IsNotNull(head, "у первой вагонетки очереди нет головы");

            return head;
        }

        [UnityTest]
        public IEnumerator QueueFrogUsesTheSameHeadAsTheOnesOnTheLoop()
        {
            yield return null;

            var model = Resources.Load<Mesh>("FrogHeadModel");

            if (model == null)
                Assert.Ignore("модели головы нет в Resources — очередь и контур оба "
                            + "собираются из шаров, проверять нечего");

            var filter = QueueHead().GetComponent<MeshFilter>();

            Assert.IsNotNull(filter, "голова в очереди собрана не из меша");
            Assert.AreSame(model, filter.sharedMesh,
                "голова в очереди взята не из FrogHeadModel: очередь и контур "
              + "показывают разных персонажей");
        }

        [UnityTest]
        public IEnumerator LevelColourLeavesTheEyesWhiteAndThePupilsDark()
        {
            yield return null;

            if (Resources.Load<Mesh>("FrogHeadModel") == null)
                Assert.Ignore("модели головы нет в Resources");

            var materials = QueueHead().GetComponent<MeshRenderer>().sharedMaterials;

            Assert.GreaterOrEqual(materials.Length, 3,
                "голова обязана раскладываться на подмеши: тело, белки, тёмное");

            // Порядок задаёт импортёр: тело, белки глаз, тёмное, рожки.
            Assert.AreEqual(Color.white, materials[1].color,
                "белки глаз перекрашены цветом уровня");
            Assert.AreNotEqual(Color.white, materials[2].color,
                "зрачки и полоса рта перекрашены в белый");
        }
    }
}
