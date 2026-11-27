using NUnit.Framework;
using Fold3DSpike.Core;

namespace Fold3DSpike.Tests
{
    // Engine-free: runs under both `dotnet test` (Core.Tests) and Unity EditMode.
    public class GameInfoTests
    {
        [Test]
        public void NameMatchesGame()
        {
            Assert.AreEqual("Fold3DSpike", GameInfo.Name);
        }
    }
}
