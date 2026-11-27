namespace Origami.Core.Content
{
    /// <summary>Fixed ending IDs (CONTENT_SPECS "엔딩 표", R-014), in priority order.</summary>
    public static class EndingIds
    {
        /// <summary>E-01 빈손: bankrupt, or final money ≤ 20.</summary>
        public const string EmptyHanded = "E-01";
        /// <summary>E-02 학의 소원: crane completions (any grade) ≥ 15.</summary>
        public const string CraneWish = "E-02";
        /// <summary>E-03 종이 재벌: final money ≥ 1000.</summary>
        public const string Tycoon = "E-03";
        /// <summary>E-04 종이접기 명인: every model has ≥ 1 ★3 completion.</summary>
        public const string Master = "E-04";
        /// <summary>E-05 동네 종이접기 가게: default.</summary>
        public const string LocalShop = "E-05";
    }
}
