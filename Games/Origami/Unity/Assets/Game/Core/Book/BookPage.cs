using System.Collections.Generic;
using System.Collections.ObjectModel;
using Origami.Core.Catalog;
using Origami.Core.Folding;

namespace Origami.Core.Book
{
    /// <summary>Content of one book page (C-160). Built by <see cref="BookPages.Get"/>. Immutable.</summary>
    public sealed class BookPage
    {
        private static readonly IReadOnlyList<ContentsEntry> NoContents = new ReadOnlyCollection<ContentsEntry>(new ContentsEntry[0]);

        internal BookPage(int index, BookPageKind kind, string modelId, bool isSealed, int stepNumber, int stepTotal,
            StepDiagram diagram, CoverInfo cover, PaperState completeShape, ContentsEntry[] contents)
        {
            Index = index;
            Kind = kind;
            ModelId = modelId;
            Sealed = isSealed;
            StepNumber = stepNumber;
            StepTotal = stepTotal;
            StepLabel = stepNumber > 0 ? BookPages.Label(stepNumber, stepTotal) : "";
            Diagram = diagram;
            Cover = cover;
            CompleteShape = completeShape;
            Contents = contents == null ? NoContents : new ReadOnlyCollection<ContentsEntry>((ContentsEntry[])contents.Clone());
        }

        public int Index { get; }
        public BookPageKind Kind { get; }

        /// <summary>Model of the page; null for pages 0 and 1.</summary>
        public string ModelId { get; }

        /// <summary>Step/Complete page of a model that is not owned ("봉인됨").</summary>
        public bool Sealed { get; }

        /// <summary>Step pages (also sealed ones): 1-based step; otherwise 0.</summary>
        public int StepNumber { get; }

        /// <summary>Step pages: the model's ActionCount; otherwise 0.</summary>
        public int StepTotal { get; }

        /// <summary>"n / N" on step pages, otherwise "".</summary>
        public string StepLabel { get; }

        /// <summary>Step page that is not sealed; otherwise null.</summary>
        public StepDiagram Diagram { get; }

        /// <summary>Cover pages only; otherwise null.</summary>
        public CoverInfo Cover { get; }

        /// <summary>Complete page that is not sealed (reference state); otherwise null.</summary>
        public PaperState CompleteShape { get; }

        /// <summary>Page 0 only (5 entries in catalogue order); otherwise empty.</summary>
        public IReadOnlyList<ContentsEntry> Contents { get; }
    }
}
