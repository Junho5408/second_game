using System;
using System.Collections.Generic;
using NUnit.Framework;
using Origami.Core.Book;
using Origami.Core.Folding;
using Origami.Core.Geometry;
using Origami.Runtime.Book;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Origami.Tests.PlayMode.BookTests
{
    /// <summary>Exact-colour pixel queries on readable textures (the rasterizer draws hard-edged, palette colours).</summary>
    internal static class Pixels
    {
        public static bool Contains(Texture2D tex, Color32 c)
        {
            foreach (var p in tex.GetPixels32())
                if (p.r == c.r && p.g == c.g && p.b == c.b && p.a == c.a) return true;
            return false;
        }

        public static bool Same(Color32 a, Color32 b) => a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;

        /// <summary>Pixel (x, y), y = 0 at the bottom (Texture2D order).</summary>
        public static Color32 At(Texture2D tex, int x, int y) => tex.GetPixels32()[y * tex.width + x];
    }

    /// <summary>ORIGAMI-013 / C-192 DiagramRaster: CPU-rasterized diagram and shape textures (CONTENT §3 notation).</summary>
    public class DiagramRasterTests
    {
        private const int Size = 256;
        private static readonly string[] AllOwned = { "plane", "house", "tulip", "dog", "cicada" };
        private readonly List<Texture2D> _made = new List<Texture2D>();

        [TearDown]
        public void TearDown()
        {
            foreach (var t in _made)
                if (t != null) Object.DestroyImmediate(t);
            _made.Clear();
        }

        private Texture2D Keep(Texture2D t)
        {
            _made.Add(t);
            return t;
        }

        private static StepDiagram DiagramOf(int page)
        {
            var d = BookPages.Get(page, AllOwned).Diagram;
            Assert.IsNotNull(d, "page " + page + " must be a step page");
            return d;
        }

        private Texture2D RenderPage(int page) => Keep(DiagramRaster.Render(DiagramOf(page), Size));

        [Test]
        public void Arguments_AreValidated()
        {
            Assert.Throws<ArgumentNullException>(() => DiagramRaster.Render(null, Size));
            Assert.Throws<ArgumentNullException>(() => DiagramRaster.RenderShape(null, 0, false, Size));
            Assert.Throws<ArgumentOutOfRangeException>(() => DiagramRaster.Render(DiagramOf(6), 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => DiagramRaster.RenderShape(PaperState.Initial, 0, false, 8));
        }

        [Test]
        public void Render_IsReadableSquareTextureOfRequestedSize()
        {
            var t = RenderPage(6);
            Assert.AreEqual(Size, t.width);
            Assert.AreEqual(Size, t.height);
            Assert.IsTrue(t.isReadable);
            var small = Keep(DiagramRaster.Render(DiagramOf(6), 64));
            Assert.AreEqual(64, small.width);
        }

        [Test]
        public void Render_IsDeterministic()
        {
            var a = RenderPage(21).GetPixels32();
            var b = RenderPage(21).GetPixels32();
            Assert.AreEqual(a.Length, b.Length);
            for (int i = 0; i < a.Length; i++)
                if (!Pixels.Same(a[i], b[i])) Assert.Fail("pixel " + i + " differs");
        }

        [Test]
        public void F007_PlaneStep4_ValleyLineArrowBothSidesAndAfterInset()
        {
            var d = DiagramOf(6);
            Assert.AreEqual(FoldStepKind.Fold, d.Step.Kind);
            Assert.IsFalse(d.Step.Mountain);
            var t = RenderPage(6);
            Assert.IsTrue(Pixels.Contains(t, DiagramRaster.Valley), "valley fold line");
            Assert.IsFalse(Pixels.Contains(t, DiagramRaster.Mountain));
            Assert.IsFalse(Pixels.Contains(t, DiagramRaster.CreaseLine));
            Assert.IsTrue(Pixels.Contains(t, DiagramRaster.Arrow), "grab → drop arrow");
            Assert.IsTrue(Pixels.Contains(t, DiagramRaster.Front), "folded flaps show the coloured side");
            Assert.IsTrue(Pixels.Contains(t, DiagramRaster.Back), "white side");
            Assert.IsTrue(Pixels.Contains(t, DiagramRaster.Mark), "the centre crease mark of step 1");
            Assert.IsTrue(Pixels.Contains(t, DiagramRaster.InsetBackground), "small 'after' picture");
            Assert.IsFalse(Pixels.Contains(t, DiagramRaster.LabelInk), "whole-stack fold has no layer label");
        }

        [Test]
        public void F007_AnchorCircles_DrawnOnlyWhenTheStepHasMagnetAnchors()
        {
            var withAnchors = DiagramOf(5); // plane step 3: P magnet (CONTENT §2)
            Assert.Greater(withAnchors.Anchors.Count, 0);
            Assert.IsTrue(Pixels.Contains(RenderPage(5), DiagramRaster.Anchor));

            var free = DiagramOf(21); // tulip step 4: free (−)
            Assert.AreEqual(0, free.Anchors.Count);
            Assert.IsFalse(Pixels.Contains(RenderPage(21), DiagramRaster.Anchor));
        }

        [Test]
        public void F007_CreaseStep_UsesFoldAndUnfoldLine_OnWhitePaper()
        {
            Assert.AreEqual(FoldStepKind.Crease, DiagramOf(3).Step.Kind);
            var t = RenderPage(3);
            Assert.IsTrue(Pixels.Contains(t, DiagramRaster.CreaseLine));
            Assert.IsFalse(Pixels.Contains(t, DiagramRaster.Valley));
            Assert.IsFalse(Pixels.Contains(t, DiagramRaster.Mountain));
            Assert.IsTrue(Pixels.Contains(t, DiagramRaster.Back), "the first sheet is white side up");
        }

        [Test]
        public void F007_MountainStep_UsesMountainLine()
        {
            var d = DiagramOf(41); // cicada step 9: mountain fold
            Assert.IsTrue(d.Step.Mountain);
            var t = RenderPage(41);
            Assert.IsTrue(Pixels.Contains(t, DiagramRaster.Mountain));
            Assert.IsFalse(Pixels.Contains(t, DiagramRaster.Valley));
        }

        [Test]
        public void F007_TopLayersStep_DrawsLayerLabel()
        {
            Assert.AreEqual(2, DiagramOf(21).Step.Layers);
            Assert.IsTrue(Pixels.Contains(RenderPage(21), DiagramRaster.LabelInk), "\"위 2겹\" label");
        }

        [Test]
        public void RenderShape_Grey_IsUniformSilhouette()
        {
            var t = Keep(DiagramRaster.RenderShape(BookPages.Get(17, new string[0]).Cover.Silhouette, 45, true, Size));
            Assert.IsTrue(Pixels.Contains(t, DiagramRaster.Grey));
            Assert.IsFalse(Pixels.Contains(t, DiagramRaster.Front));
            Assert.IsFalse(Pixels.Contains(t, DiagramRaster.Back));
            Assert.IsFalse(Pixels.Contains(t, DiagramRaster.Edge));
        }

        [Test]
        public void RenderShape_Owned_UsesPaperColoursAndEdges()
        {
            var t = Keep(DiagramRaster.RenderShape(BookPages.Get(9, AllOwned).CompleteShape, 0, false, Size));
            Assert.IsFalse(Pixels.Contains(t, DiagramRaster.Grey));
            Assert.IsTrue(Pixels.Contains(t, DiagramRaster.Front) || Pixels.Contains(t, DiagramRaster.Back));
            Assert.IsTrue(Pixels.Contains(t, DiagramRaster.Edge));
        }

        [Test]
        public void RenderShape_PaperYIsTextureUp_AndRotationChangesThePicture()
        {
            // Two half-sheets: top half coloured side up, bottom half white side up.
            var top = new Facet(new[] { new Vec2(-1, 0), new Vec2(1, 0), new Vec2(1, 1), new Vec2(-1, 1) },
                new[] { new Vec2(-1, 0), new Vec2(1, 0), new Vec2(1, 1), new Vec2(-1, 1) }, 0, true);
            var bottom = new Facet(new[] { new Vec2(-1, -1), new Vec2(1, -1), new Vec2(1, 0), new Vec2(-1, 0) },
                new[] { new Vec2(-1, -1), new Vec2(1, -1), new Vec2(1, 0), new Vec2(-1, 0) }, 0, false);
            var state = new PaperState(new[] { top, bottom }, new CreaseSegment[0]);

            var t = Keep(DiagramRaster.RenderShape(state, 0, false, Size));
            Assert.IsTrue(Pixels.Same(DiagramRaster.Front, Pixels.At(t, Size / 2, Size * 3 / 4)), "paper +y is up in the texture");
            Assert.IsTrue(Pixels.Same(DiagramRaster.Back, Pixels.At(t, Size / 2, Size / 4)));

            var r = Keep(DiagramRaster.RenderShape(state, 90, false, Size));
            Assert.IsTrue(Pixels.Same(DiagramRaster.Front, Pixels.At(r, Size / 4, Size / 2)), "90° counter-clockwise: top half → left");
            Assert.IsTrue(Pixels.Same(DiagramRaster.Back, Pixels.At(r, Size * 3 / 4, Size / 2)));
        }

        [Test]
        public void Symbols_ContainEveryNotation()
        {
            var t = Keep(DiagramRaster.RenderSymbols(Size));
            Assert.IsTrue(Pixels.Contains(t, DiagramRaster.Valley));
            Assert.IsTrue(Pixels.Contains(t, DiagramRaster.Mountain));
            Assert.IsTrue(Pixels.Contains(t, DiagramRaster.CreaseLine));
            Assert.IsTrue(Pixels.Contains(t, DiagramRaster.Arrow));
            Assert.IsTrue(Pixels.Contains(t, DiagramRaster.LabelInk));
            Assert.IsTrue(Pixels.Contains(t, DiagramRaster.Mark));
            Assert.IsTrue(Pixels.Contains(t, DiagramRaster.Anchor));
        }

        [Test]
        public void EveryOwnedPicturePage_Renders()
        {
            var go = new GameObject("BookPanelAllPages");
            try
            {
                var panel = go.AddComponent<BookPanel>();
                var host = new FakeBookHost();
                host.Owned.AddRange(new[] { "tulip", "dog", "cicada" });
                panel.Bind(host);
                for (int page = 0; page < Origami.Core.Catalog.BookLayout.PageCount; page++)
                {
                    var tex = panel.DiagramTexture(page);
                    if (page == 0) Assert.IsNull(tex, "contents");
                    else Assert.IsNotNull(tex, "page " + page);
                }
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
