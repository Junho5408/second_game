using System;
using System.Collections.Generic;
using System.Globalization;
using Origami.Core.Catalog;
using Origami.Core.Folding;
using Origami.Core.Geometry;

namespace Origami.Core.Book
{
    /// <summary>
    /// Book page contents (C-160, R-010, F-007, F-008, CONTENT §3). Pure: the page content is not game state, it is
    /// recomputed from (page, owned pages). Diagrams come from the reference sequences run through the Core engine (D-111).
    /// </summary>
    public static class BookPages
    {
        /// <summary>Content of page 0..43 for the owned model IDs (Ordinal; null/unknown/duplicate entries are ignored).</summary>
        /// <exception cref="ArgumentOutOfRangeException">page not in 0..BookLayout.PageCount − 1.</exception>
        /// <exception cref="ArgumentNullException">ownedModelIds is null.</exception>
        public static BookPage Get(int page, IEnumerable<string> ownedModelIds)
        {
            if (page < 0 || page >= BookLayout.PageCount) throw new ArgumentOutOfRangeException(nameof(page));
            if (ownedModelIds == null) throw new ArgumentNullException(nameof(ownedModelIds));
            var owned = OwnedFlags(ownedModelIds);

            var kind = BookLayout.KindOf(page);
            switch (kind)
            {
                case BookPageKind.Contents:
                    return new BookPage(page, kind, null, false, 0, 0, null, null, null, Contents(owned));
                case BookPageKind.Symbols:
                    return new BookPage(page, kind, null, false, 0, 0, null, null, null, null);
            }

            string id = BookLayout.ModelAt(page);
            int m = ModelCatalog.IndexOf(id);
            var model = ModelCatalog.Models[m];
            bool isOwned = owned[m];
            switch (kind)
            {
                case BookPageKind.Cover:
                    var cover = new CoverInfo(model.Id, model.DisplayName, ModelCatalog.ReferenceState(model.Id), model.CoverRotationDeg,
                        model.Difficulty, model.ActionCount, model.Techniques, model.BasePrice, model.PagePrice, isOwned);
                    return new BookPage(page, kind, id, false, 0, 0, null, cover, null, null);
                case BookPageKind.Step:
                    int step = BookLayout.StepAt(page);
                    return new BookPage(page, kind, id, !isOwned, step, model.ActionCount,
                        isOwned ? Diagram(model, step) : null, null, null, null);
                default:
                    return new BookPage(page, kind, id, !isOwned, 0, 0, null, null,
                        isOwned ? ModelCatalog.ReferenceState(id) : null, null);
            }
        }

        internal static string Label(int step, int total) =>
            step.ToString(CultureInfo.InvariantCulture) + " / " + total.ToString(CultureInfo.InvariantCulture);

        private static bool[] OwnedFlags(IEnumerable<string> ids)
        {
            var flags = new bool[ModelCatalog.Models.Count];
            foreach (var id in ids)
            {
                int i = ModelCatalog.IndexOf(id);
                if (i >= 0) flags[i] = true;
            }
            return flags;
        }

        private static ContentsEntry[] Contents(bool[] owned)
        {
            var models = ModelCatalog.Models;
            var entries = new ContentsEntry[models.Count];
            for (int i = 0; i < models.Count; i++)
                entries[i] = new ContentsEntry(models[i].Id, models[i].DisplayName, owned[i], models[i].PagePrice,
                    BookLayout.CoverPage(models[i].Id));
            return entries;
        }

        private static StepDiagram Diagram(ModelDefinition model, int stepNumber)
        {
            var step = model.Steps[stepNumber - 1];
            var before = ModelCatalog.StateBeforeStep(model.Id, stepNumber);
            var after = ModelCatalog.StateBeforeStep(model.Id, stepNumber + 1);
            int folds = 0, creases = 0;
            for (int i = 0; i < stepNumber - 1; i++)
            {
                if (model.Steps[i].Kind == FoldStepKind.Fold) folds++;
                else if (model.Steps[i].Kind == FoldStepKind.Crease) creases++;
            }
            // Reference steps always cross the paper (catalogue tests, D-111); a missing line would be a catalogue bug.
            if (!LineOnPaper.TryGet(before, step.Grab, step.Drop, out var a, out var b))
                throw new InvalidOperationException("reference step without a fold line: " + model.Id + " " + stepNumber);
            var snap = DropSnap.Snap(before, step.Grab, step.Drop);
            Vec2[] anchors;
            switch (snap.Kind)
            {
                case SnapKind.Point: anchors = new[] { snap.AnchorA }; break;
                case SnapKind.Line: anchors = new[] { snap.AnchorA, snap.AnchorB }; break;
                default: anchors = new Vec2[0]; break;
            }
            return new StepDiagram(step, before, after, folds, creases, a, b, snap.Kind, anchors, model.StepTexts[stepNumber - 1]);
        }
    }
}
