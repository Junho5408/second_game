using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using {{GAME}}.Runtime;

namespace {{GAME}}.Tests.PlayMode
{
    public class BootstrapTests
    {
        [UnityTest]
        public IEnumerator BootstrapCreatesCamera()
        {
            yield return null;
            Assert.IsNotNull(Camera.main, "Bootstrap should create a main camera");
            Assert.IsNotNull(GameObject.Find(Bootstrap.RootName), "Bootstrap root object missing");
        }
    }
}
