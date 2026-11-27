using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Fold3DSpike.Runtime.Gameplay;

namespace Fold3DSpike.Tests.PlayMode
{
    public class FoldSpikeTests
    {
        [UnityTest]
        public IEnumerator ControllerExistsOnce()
        {
            yield return null;
            var a = FoldSpikeController.EnsureInstance();
            var b = FoldSpikeController.EnsureInstance();
            Assert.AreSame(a, b);
            Assert.AreEqual(1, Object.FindObjectsByType<FoldSpikeController>(FindObjectsSortMode.None).Length);
            Assert.IsNotNull(GameObject.Find(FoldSpikeController.ObjectName));
        }

        [UnityTest]
        public IEnumerator ProgrammaticFoldChangesGeometryAndScores()
        {
            yield return null;
            var c = FoldSpikeController.EnsureInstance();
            c.Restart();
            int before = c.PaperVertexCount;
            Assert.Greater(before, 0);

            Assert.IsTrue(c.Fold(new Vector2(-1f, 1f), new Vector2(0.1f, -0.05f), false, false));
            Assert.AreNotEqual(before, c.PaperVertexCount);
            Assert.AreEqual(2, c.Session.State.Facets.Count);
            Assert.IsNotNull(c.Session.LastScore);
            Assert.That(c.Session.LastScore.Score, Is.InRange(0, 100));

            Assert.IsFalse(c.Fold(new Vector2(0f, 0f), new Vector2(0.001f, 0f), false, false), "tiny drag is no fold");

            c.Undo();
            Assert.AreEqual(before, c.PaperVertexCount);
            c.Restart();
        }
    }
}
