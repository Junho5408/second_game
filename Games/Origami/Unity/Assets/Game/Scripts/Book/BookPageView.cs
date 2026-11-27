using System.Collections.Generic;
using Origami.Core.Book;
using Origami.Core.Catalog;

namespace Origami.Runtime.Book
{
    /// <summary>What a page button does (<see cref="BookPanel.Press"/>).</summary>
    internal enum BookButtonKind
    {
        Contents = 0,   // → BookPanel.ClickContents(ModelId)
        Buy = 1,        // → BookPanel.ClickBuy(ModelId)
        TurnBack = 2,   // → BookPanel.ClickTurn(−2)
        TurnForward = 3,// → BookPanel.ClickTurn(+2)
    }

    /// <summary>One button of the book (label + what it does). Immutable.</summary>
    internal sealed class BookButton
    {
        public BookButton(BookButtonKind kind, string label, string modelId)
        {
            Kind = kind;
            Label = label;
            ModelId = modelId;
        }

        public BookButtonKind Kind { get; }
        public string Label { get; }
        public string ModelId { get; }
    }

    /// <summary>
    /// Pre-built drawing model of one page (made in <see cref="BookPanel.Prepare"/>, drawn by <see cref="BookPanel.Draw"/>):
    /// title, text lines, buttons, and whether a picture (<see cref="BookPanel.DiagramTexture"/>) belongs to the page.
    /// Strings are built outside OnGUI (CODING_CONVENTION UI); tests read this model instead of screen pixels.
    /// </summary>
    internal sealed class BookPageView
    {
        public BookPageView(BookPage page, string title, List<string> lines, List<BookButton> buttons, bool hasPicture)
        {
            Page = page;
            Title = title ?? "";
            Lines = lines.AsReadOnly();
            Buttons = buttons.AsReadOnly();
            HasPicture = hasPicture;
        }

        public BookPage Page { get; }
        public string Title { get; }
        public IReadOnlyList<string> Lines { get; }
        public IReadOnlyList<BookButton> Buttons { get; }
        public bool HasPicture { get; }

        /// <summary>Builds the view of a page; <paramref name="money"/> is shown on buyable covers.</summary>
        public static BookPageView Build(BookPage page, int money)
        {
            var lines = new List<string>();
            var buttons = new List<BookButton>();
            string title = "";
            bool picture = false;
            string name = page.ModelId != null ? NameOf(page.ModelId) : "";

            switch (page.Kind)
            {
                case BookPageKind.Contents:
                    title = BookTexts.ContentsTitle;
                    foreach (var e in page.Contents)
                        buttons.Add(new BookButton(BookButtonKind.Contents, BookTexts.ContentsLabel(e), e.ModelId));
                    break;
                case BookPageKind.Symbols:
                    title = BookTexts.SymbolsTitle;
                    lines.AddRange(BookTexts.SymbolLabels);
                    picture = true;
                    break;
                case BookPageKind.Cover:
                    var c = page.Cover;
                    title = c.Name;
                    picture = true;
                    lines.Add(BookTexts.Difficulty(c.Difficulty));
                    lines.Add(BookTexts.ActionCount(c.ActionCount));
                    lines.Add(BookTexts.Techniques(c.Techniques));
                    lines.Add(BookTexts.BasePrice(c.BasePrice));
                    if (c.Owned)
                    {
                        lines.Add(BookTexts.Owned);
                    }
                    else
                    {
                        lines.Add(BookTexts.PagePrice(c.PagePrice));
                        lines.Add(BookTexts.Money(money));
                    }
                    if (c.Buyable) buttons.Add(new BookButton(BookButtonKind.Buy, BookTexts.Buy, c.ModelId));
                    break;
                case BookPageKind.Step:
                    if (page.Sealed)
                    {
                        lines.Add(BookTexts.Sealed);
                        break;
                    }
                    title = name + "  " + page.StepLabel;
                    picture = page.Diagram != null;
                    if (page.Diagram != null)
                    {
                        lines.Add(BookTexts.ActionLine(page.Diagram.Step));
                        lines.Add(page.Diagram.Text ?? "");
                    }
                    break;
                default: // Complete
                    if (page.Sealed)
                    {
                        lines.Add(BookTexts.Sealed);
                        break;
                    }
                    title = BookTexts.CompleteTitle(name);
                    picture = page.CompleteShape != null;
                    var model = ModelCatalog.Find(page.ModelId);
                    if (model != null) lines.Add(BookTexts.CompletePrice(model.BasePrice));
                    break;
            }
            return new BookPageView(page, title, lines, buttons, picture);
        }

        private static string NameOf(string id)
        {
            var m = ModelCatalog.Find(id);
            return m != null ? m.DisplayName : "";
        }
    }
}
