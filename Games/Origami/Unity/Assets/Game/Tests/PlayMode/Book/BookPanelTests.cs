using System.Linq;
using NUnit.Framework;
using Origami.Core.Catalog;
using Origami.Runtime.Book;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Origami.Tests.PlayMode.BookTests
{
    /// <summary>
    /// ORIGAMI-013 / C-192: BookPanel spread, page views, fixed CONTENT §3 texts, Click… → IBookHost callbacks, texture
    /// cache. Plain [Test]s: no frame waits, no screen pixels.
    /// </summary>
    public class BookPanelTests
    {
        // CONTENT §3 fixed book texts, copied verbatim from the spec (review ORIGAMI-011 W-2) — not from BookTexts.
        private const string SealedText = "봉인됨 — 표지에서 페이지를 사세요";
        private const string OwnedText = "보유";

        private GameObject _go;
        private BookPanel _panel;
        private FakeBookHost _host;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("BookPanelTest");
            _panel = _go.AddComponent<BookPanel>();
            _host = new FakeBookHost();
            _panel.Bind(_host);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            _go = null;
        }

        private void At(int page)
        {
            _host.Page = page;
            _panel.Prepare();
        }

        [Test]
        public void F007_FirstSpread_IsContentsAndSymbols()
        {
            At(0);
            Assert.AreEqual(0, _panel.LeftPage.Index);
            Assert.AreEqual(BookPageKind.Contents, _panel.LeftPage.Kind);
            Assert.AreEqual(1, _panel.RightPage.Index);
            Assert.AreEqual(BookPageKind.Symbols, _panel.RightPage.Kind);
            Assert.AreEqual("목차", _panel.LeftView.Title);
            Assert.AreEqual(6, _panel.RightView.Lines.Count);
            Assert.IsNull(_panel.DiagramTexture(0), "contents page has no picture");
            Assert.IsNotNull(_panel.DiagramTexture(1), "symbols page is always visible (F-007 AC-03)");
        }

        [Test]
        public void F007_Contents_ShowsOwnedOrPagePriceAndCoverPage_FixedTexts()
        {
            At(0);
            var labels = _panel.LeftView.Buttons.Select(b => b.Label).ToArray();
            CollectionAssert.AreEqual(new[]
            {
                "종이비행기  " + OwnedText + "  2쪽",
                "집  " + OwnedText + "  10쪽",
                "튤립  20원  17쪽",
                "강아지 얼굴  50원  24쪽",
                "매미  150원  32쪽",
            }, labels);
            CollectionAssert.AreEqual(new[] { "plane", "house", "tulip", "dog", "cicada" },
                _panel.LeftView.Buttons.Select(b => b.ModelId).ToArray());
            Assert.IsTrue(_panel.LeftView.Buttons.All(b => b.Kind == BookButtonKind.Contents));
        }

        [Test]
        public void F007_Contents_AfterBuying_ShowsOwned()
        {
            _host.Owned.Add("tulip");
            At(0);
            StringAssert.Contains(OwnedText, _panel.LeftView.Buttons[2].Label);
            StringAssert.DoesNotContain("20원", _panel.LeftView.Buttons[2].Label);
        }

        [Test]
        public void R010_ContentsClick_TurnsToCoverSpread()
        {
            At(0);
            _panel.Press(_panel.LeftView.Buttons[2]);
            CollectionAssert.AreEqual(new[] { 17 }, _host.TurnCalls);
            _panel.Prepare();
            Assert.AreEqual(16, _panel.LeftPage.Index);
            Assert.AreEqual(17, _panel.RightPage.Index);
            Assert.AreEqual(BookPageKind.Cover, _panel.RightPage.Kind);
            Assert.AreEqual("tulip", _panel.RightPage.ModelId);
        }

        [Test]
        public void ClickContents_UnknownOrNull_DoesNotCallHost()
        {
            _panel.ClickContents("unicorn");
            _panel.ClickContents(null);
            _panel.ClickContents("");
            Assert.AreEqual(0, _host.TurnCalls.Count);
            _panel.ClickContents("cicada");
            CollectionAssert.AreEqual(new[] { 32 }, _host.TurnCalls);
        }

        [Test]
        public void F008_UnownedCover_HasBuyButton_ThatCallsHostBuyPage()
        {
            At(16);
            var cover = _panel.RightView;
            Assert.AreEqual("튤립", cover.Title);
            Assert.AreEqual(1, cover.Buttons.Count);
            Assert.AreEqual(BookButtonKind.Buy, cover.Buttons[0].Kind);
            Assert.AreEqual("사기", cover.Buttons[0].Label);
            Assert.AreEqual("tulip", cover.Buttons[0].ModelId);
            CollectionAssert.Contains(cover.Lines, "페이지 가격 20원");
            CollectionAssert.Contains(cover.Lines, "기본가 12원");
            CollectionAssert.Contains(cover.Lines, "난이도 ★★");

            _panel.Press(cover.Buttons[0]);
            CollectionAssert.AreEqual(new[] { "tulip" }, _host.BuyCalls);
            Assert.AreEqual(0, _host.TurnCalls.Count);
        }

        [Test]
        public void F008_OwnedCover_HasNoBuyButton()
        {
            At(2);
            Assert.AreEqual(BookPageKind.Cover, _panel.LeftPage.Kind);
            Assert.AreEqual(0, _panel.LeftView.Buttons.Count);
            CollectionAssert.Contains(_panel.LeftView.Lines, OwnedText);
        }

        [Test]
        public void ClickBuy_ForwardsAnyIdToHost_CoreDecides()
        {
            _panel.ClickBuy("unicorn");
            _panel.ClickBuy(null);
            CollectionAssert.AreEqual(new[] { "unicorn", null }, _host.BuyCalls);
        }

        [Test]
        public void F007_SealedStepAndCompletePages_ShowOnlyTheFixedSealedText()
        {
            At(18);
            Assert.IsTrue(_panel.LeftPage.Sealed);
            CollectionAssert.AreEqual(new[] { SealedText }, _panel.LeftView.Lines);
            Assert.AreEqual("", _panel.LeftView.Title);
            Assert.AreEqual(0, _panel.LeftView.Buttons.Count);
            Assert.IsNull(_panel.DiagramTexture(18));
            Assert.IsNull(_panel.DiagramTexture(19));

            At(22);
            Assert.AreEqual(BookPageKind.Complete, _panel.RightPage.Kind);
            Assert.IsTrue(_panel.RightPage.Sealed);
            CollectionAssert.AreEqual(new[] { SealedText }, _panel.RightView.Lines);
            Assert.IsNull(_panel.DiagramTexture(23));
        }

        [Test]
        public void F007_AfterOwning_StepPageShowsDiagramLabelAndText()
        {
            At(18);
            _host.Owned.Add("tulip");
            _panel.Prepare();
            Assert.IsFalse(_panel.LeftPage.Sealed);
            Assert.IsNotNull(_panel.LeftPage.Diagram);
            Assert.IsNotNull(_panel.DiagramTexture(18));
            Assert.AreEqual("튤립  1 / 5", _panel.LeftView.Title);
            CollectionAssert.Contains(_panel.LeftView.Lines, _panel.LeftPage.Diagram.Text);
            CollectionAssert.Contains(_panel.LeftView.Lines, "골접기 · 전체");
        }

        [Test]
        public void F007_TopLayersStep_ShowsLayerLabel()
        {
            _host.Owned.Add("tulip");
            At(20); // tulip step 4 is page 21: top 2 layers (F-007 AC-02)
            Assert.AreEqual(21, _panel.RightPage.Index);
            Assert.AreEqual(2, _panel.RightPage.Diagram.Step.Layers);
            CollectionAssert.Contains(_panel.RightView.Lines, "골접기 · 위 2겹");
        }

        [Test]
        public void F007_CreaseStep_ShowsFoldAndUnfold()
        {
            At(2); // plane step 1 = page 3, a crease
            CollectionAssert.Contains(_panel.RightView.Lines, "접었다 펴기 · 전체");
            Assert.AreEqual("종이비행기  1 / 6", _panel.RightView.Title);
        }

        [Test]
        public void F007_CompletePage_ShowsFixedBasePriceText()
        {
            At(8); // plane complete = page 9
            Assert.AreEqual(BookPageKind.Complete, _panel.RightPage.Kind);
            CollectionAssert.AreEqual(new[] { "판매대에서 기본 6원" }, _panel.RightView.Lines);
            Assert.AreEqual("종이비행기 완성", _panel.RightView.Title);
            Assert.IsNotNull(_panel.DiagramTexture(9));

            At(16); // house complete = page 16
            Assert.AreEqual(BookPageKind.Complete, _panel.LeftPage.Kind);
            CollectionAssert.AreEqual(new[] { "판매대에서 기본 8원" }, _panel.LeftView.Lines);
        }

        [Test]
        public void R010_OddHostPage_ShowsItsSpread()
        {
            At(17);
            Assert.AreEqual(16, _panel.LeftPage.Index);
            Assert.AreEqual(17, _panel.RightPage.Index);
            At(43);
            Assert.AreEqual(42, _panel.LeftPage.Index);
            Assert.AreEqual(43, _panel.RightPage.Index);
        }

        [Test]
        public void TurnButtons_CallHostWithCurrentPlusMinusTwo()
        {
            At(0);
            Assert.IsFalse(_panel.CanTurnBack);
            Assert.IsTrue(_panel.CanTurnForward);
            _panel.Press(_panel.SpreadButtons[1]);
            CollectionAssert.AreEqual(new[] { 2 }, _host.TurnCalls);
            _panel.Press(_panel.SpreadButtons[0]);
            CollectionAssert.AreEqual(new[] { 2, 0 }, _host.TurnCalls);
            _panel.ClickTurn(-2);
            CollectionAssert.AreEqual(new[] { 2, 0, -2 }, _host.TurnCalls, "the panel forwards; Core clamps (R-010)");

            At(42);
            Assert.IsTrue(_panel.CanTurnBack);
            Assert.IsFalse(_panel.CanTurnForward);
        }

        [Test]
        public void ClickTurn_SaturatesInsteadOfOverflowing()
        {
            _host.Page = int.MaxValue;
            _panel.ClickTurn(2);
            Assert.AreEqual(int.MaxValue, _host.TurnCalls[0]);
        }

        [Test]
        public void Prepare_IsCachedUntilPageOwnedOrMoneyChanges()
        {
            At(16);
            var left = _panel.LeftPage;
            _panel.Prepare();
            Assert.AreSame(left, _panel.LeftPage);

            _host.Cash = 35;
            _panel.Prepare();
            CollectionAssert.Contains(_panel.RightView.Lines, "가진 돈 35원");
        }

        [Test]
        public void Textures_AreCachedPerPageAndOwnedState()
        {
            At(6);
            var step = _panel.DiagramTexture(6);
            Assert.IsNotNull(step);
            Assert.AreEqual(BookPanel.TextureSize, step.width);
            At(16);
            Assert.AreSame(step, _panel.DiagramTexture(6), "same page + same owned state → cached texture");

            var greyCover = _panel.DiagramTexture(17);
            Assert.IsNotNull(greyCover);
            Assert.IsTrue(Pixels.Contains(greyCover, DiagramRaster.Grey));
            Assert.IsFalse(Pixels.Contains(greyCover, DiagramRaster.Front));

            _host.Owned.Add("tulip");
            _panel.Prepare();
            var cover = _panel.DiagramTexture(17);
            Assert.AreNotSame(greyCover, cover);
            Assert.IsFalse(Pixels.Contains(cover, DiagramRaster.Grey));
            Assert.IsTrue(Pixels.Contains(cover, DiagramRaster.Front) || Pixels.Contains(cover, DiagramRaster.Back));
            Assert.AreSame(step, _panel.DiagramTexture(6), "buying tulip does not change plane pictures");
        }

        [Test]
        public void DiagramTexture_OutOfRange_IsNull()
        {
            Assert.IsNull(_panel.DiagramTexture(-1));
            Assert.IsNull(_panel.DiagramTexture(BookLayout.PageCount));
        }

        [Test]
        public void Unbound_Panel_DoesNothingAndDoesNotThrow()
        {
            var go = new GameObject("BookPanelUnbound");
            try
            {
                var panel = go.AddComponent<BookPanel>();
                Assert.DoesNotThrow(() =>
                {
                    panel.Prepare();
                    panel.ClickBuy("tulip");
                    panel.ClickContents("tulip");
                    panel.ClickTurn(2);
                });
                Assert.IsNull(panel.DiagramTexture(6));

                panel.Bind(_host);
                panel.Bind(null);
                panel.ClickBuy("tulip");
                Assert.AreEqual(0, _host.BuyCalls.Count);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void F008_WithScreenFlow_BuyFromCover_UnsealsPagesAndRemovesButton()
        {
            var flowHost = new FlowBookHost();
            Assert.IsTrue(flowHost.Flow.Interact(InteractTargets.Bookshelf).Accepted);
            _panel.Bind(flowHost);
            _panel.Prepare();
            Assert.AreEqual(0, _panel.LeftPage.Index);

            _panel.Press(_panel.LeftView.Buttons.First(b => b.ModelId == "tulip"));
            Assert.AreEqual(16, flowHost.Flow.Snapshot.BookPage);
            _panel.Prepare();
            Assert.AreEqual(17, _panel.RightPage.Index);

            _panel.Press(_panel.RightView.Buttons.Single(b => b.Kind == BookButtonKind.Buy));
            Assert.AreEqual(0, flowHost.Flow.Snapshot.Money, "F-008 AC-01");
            CollectionAssert.Contains(flowHost.Flow.Snapshot.OwnedModelIds, "tulip");
            Assert.AreEqual(20, flowHost.Flow.Snapshot.TotalBookCost);

            _panel.Prepare();
            Assert.AreEqual(0, _panel.RightView.Buttons.Count);
            CollectionAssert.Contains(_panel.RightView.Lines, OwnedText);
            Assert.IsNotNull(_panel.DiagramTexture(18), "F-007 AC-03: diagram after BuyPage(tulip)");
        }

        [Test]
        public void F008_WithScreenFlow_NotEnoughMoney_StaysSealed()
        {
            var flowHost = new FlowBookHost();
            flowHost.Flow.Interact(InteractTargets.Bookshelf);
            _panel.Bind(flowHost);
            _panel.ClickContents("dog");
            _panel.Prepare();
            Assert.AreEqual(24, _panel.LeftPage.Index);
            _panel.Press(_panel.LeftView.Buttons.Single(b => b.Kind == BookButtonKind.Buy));
            Assert.AreEqual(20, flowHost.Flow.Snapshot.Money, "F-008 AC-03");
            _panel.Prepare();
            Assert.AreEqual(1, _panel.LeftView.Buttons.Count);
            CollectionAssert.AreEqual(new[] { SealedText }, _panel.RightView.Lines);
        }

        [Test]
        public void OnDestroy_DestroysCachedTextures()
        {
            At(6);
            var tex = _panel.DiagramTexture(6);
            Assert.IsNotNull(tex);
            Assert.Greater(_panel.CachedTextureCount, 0);
            Object.DestroyImmediate(_go);
            _go = null;
            Assert.IsTrue(tex == null, "texture destroyed with the panel");
        }
    }
}
