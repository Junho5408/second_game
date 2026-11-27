using System.Linq;
using NUnit.Framework;
using Origami.Core.Catalog;

namespace Origami.Tests.Catalog
{
    // C-120 EndingCatalog (D-114 = CONTENT §8) and InteractTargets (R-004).
    public class EndingAndTargetTests
    {
        [Test]
        public void Endings_MatchContent8()
        {
            var e = EndingCatalog.Endings;
            CollectionAssert.AreEqual(new[] { "E-01", "E-02", "E-03", "E-04", "E-05" }, e.Select(x => x.Id));
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5 }, e.Select(x => x.Priority));
            CollectionAssert.AreEqual(new[] { "빈손", "튤립 꽃밭", "종이접기 명인", "종이 재벌", "동네 종이접기 공방" }, e.Select(x => x.DisplayName));
            Assert.AreEqual("종이는 다 떨어졌지만, 종이접기는 취미로 남았다.", e[0].Text);
            Assert.AreEqual("공방 창가가 종이 튤립으로 가득 찼다. 사람들이 꽃밭 공방이라 부르기 시작했다.", e[1].Text);
            Assert.AreEqual("책 없이도 손이 먼저 움직인다. 사람들은 당신을 명인이라 부른다.", e[2].Text);
            Assert.AreEqual("종이 한 장으로 시작해 거리의 명물 공방이 되었다.", e[3].Text);
            Assert.AreEqual("소박하지만 단골이 생긴 동네 공방. 다음 주도 문을 연다.", e[4].Text);
            CollectionAssert.AreEqual(e.Select(x => x.Id),
                new[] { EndingIds.EmptyHanded, EndingIds.TulipGarden, EndingIds.Master, EndingIds.Tycoon, EndingIds.LocalWorkshop });
            Assert.AreSame(e[2], EndingCatalog.Find("E-03"));
            Assert.IsNull(EndingCatalog.Find("E-06"));
            Assert.IsNull(EndingCatalog.Find(null));
            Assert.IsNull(EndingCatalog.Find("e-01"));
        }

        [Test]
        public void InteractTargets_OrderAndValidation()
        {
            CollectionAssert.AreEqual(new[] { "table", "bookshelf", "counter", "bed" }, InteractTargets.All);
            CollectionAssert.AreEqual(InteractTargets.All,
                new[] { InteractTargets.Table, InteractTargets.Bookshelf, InteractTargets.Counter, InteractTargets.Bed });
            foreach (var t in InteractTargets.All) Assert.IsTrue(InteractTargets.IsValid(t));
            Assert.IsFalse(InteractTargets.IsValid(null));
            Assert.IsFalse(InteractTargets.IsValid(""));
            Assert.IsFalse(InteractTargets.IsValid("Table"));
            Assert.IsFalse(InteractTargets.IsValid("trash"));
        }
    }
}
