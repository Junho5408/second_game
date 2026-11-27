namespace Origami.Core.Folding
{
    /// <summary>Why a fold/crease is invalid (C-103). Texts: <see cref="FoldTexts.ErrorMessage"/>.</summary>
    public enum FoldError
    {
        None = 0,
        /// <summary>Distance(grab, drop) &lt; MinDrag — "조금 더 끌어 주세요".</summary>
        DragTooShort,
        /// <summary>layers ≥ 1 but no facet contains grab — "잡은 곳에 종이가 없어요".</summary>
        NothingGrabbed,
        /// <summary>Moving set is empty — "접히는 부분이 없어요".</summary>
        NothingToFold,
        /// <summary>Nothing stays — "접는 선이 종이 밖이에요".</summary>
        LineOutsidePaper,
        /// <summary>layers &lt; 0 (the UI never produces it; text "").</summary>
        InvalidLayers,
    }
}
