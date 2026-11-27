using NUnit.Framework;
using Origami.Core.Room;
using Origami.Tests.Folding;

namespace Origami.Tests.Room
{
    // C-180: pins the public surface (a change needs a contract change request).
    public class RoomSurfaceTests
    {
        [Test]
        public void Namespace_ContainsOnlyContractTypes()
        {
            SurfaceAssert.NamespaceHasOnly(typeof(RoomMotion).Assembly, "Origami.Core.Room",
                "Box", "FurnitureSpec", "Interaction", "RoomLayout", "RoomMotion");
        }

        [Test]
        public void Box_Surface()
        {
            var t = typeof(Box);
            Assert.IsTrue(t.IsValueType);
            SurfaceAssert.Is(t, ".ctor(Double,Double,Double,Double)",
                "Double MinX {get;}", "Double MinZ {get;}", "Double MaxX {get;}", "Double MaxZ {get;}");
        }

        [Test]
        public void FurnitureSpec_Surface()
        {
            var t = typeof(FurnitureSpec);
            Assert.IsTrue(t.IsSealed && !t.IsAbstract);
            SurfaceAssert.Is(t, "String Id {get;}", "Vec2 Center {get;}", "Double Width {get;}", "Double Depth {get;}",
                "Double Height {get;}", "Boolean Blocks {get;}");
        }

        [Test]
        public void RoomLayout_Surface()
        {
            var t = typeof(RoomLayout);
            Assert.IsTrue(t.IsSealed && t.IsAbstract, "static");
            SurfaceAssert.Is(t,
                "const Double MinX", "const Double MaxX", "const Double MinZ", "const Double MaxZ", "const Double WallHeight",
                "const Double CharacterRadius", "const Double WalkSpeed", "const Double InteractRange", "const Double CharacterHeight",
                "static Vec2 StartPosition {get;}", "static Vec2 ChairPosition {get;}", "static Vec2 StandUpPosition {get;}",
                "static IReadOnlyList`1 Furniture {get;}", "static IReadOnlyList`1 Obstacles {get;}",
                "static Vec2 InteractPoint(String)");
        }

        [Test]
        public void RoomMotion_Surface()
        {
            var t = typeof(RoomMotion);
            Assert.IsTrue(t.IsSealed && t.IsAbstract, "static");
            SurfaceAssert.Is(t, "const Double MaxSubstep", "static Vec2 Step(Vec2,Vec2,Double)");
        }

        [Test]
        public void Interaction_Surface()
        {
            var t = typeof(Interaction);
            Assert.IsTrue(t.IsSealed && t.IsAbstract, "static");
            SurfaceAssert.Is(t, "static String Nearest(Vec2)");
        }
    }
}
