using NUnit.Framework;
using Sandbox.Core;

namespace Sandbox.Tests
{
    // Engine-free: runs under both `dotnet test` (Core.Tests) and Unity EditMode.
    public class GameInfoTests
    {
        [Test]
        public void NameMatchesGame()
        {
            Assert.AreEqual("Sandbox", GameInfo.Name);
        }
    }
}
