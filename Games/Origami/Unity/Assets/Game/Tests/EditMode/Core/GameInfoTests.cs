using NUnit.Framework;
using Origami.Core;

namespace Origami.Tests
{
    // Engine-free: runs under both `dotnet test` (Core.Tests) and Unity EditMode.
    public class GameInfoTests
    {
        [Test]
        public void NameMatchesGame()
        {
            Assert.AreEqual("Origami", GameInfo.Name);
        }
    }
}
