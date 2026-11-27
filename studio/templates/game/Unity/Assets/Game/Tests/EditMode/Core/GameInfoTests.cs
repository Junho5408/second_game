using NUnit.Framework;
using {{GAME}}.Core;

namespace {{GAME}}.Tests
{
    // Engine-free: runs under both `dotnet test` (Core.Tests) and Unity EditMode.
    public class GameInfoTests
    {
        [Test]
        public void NameMatchesGame()
        {
            Assert.AreEqual("{{GAME}}", GameInfo.Name);
        }
    }
}
