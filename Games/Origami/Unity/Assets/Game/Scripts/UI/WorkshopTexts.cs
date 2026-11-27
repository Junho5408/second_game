namespace Origami.Runtime.UI
{
    /// <summary>
    /// Fixed dialog and button texts of the workshop GUI (DECISIONS 2026-11-27, review ORIGAMI-011 W-2): copied from FEATURE_SPECS
    /// F-003 (pause menu "계속 / 새로 시작", new-run confirm, sleep confirm "Enter = 예, Esc = 아니오", settlement "다음 날")
    /// and F-013 (seated button names). Game-rule texts (HUD, hints, rejections, settlement / counter / result lines) stay in
    /// Core <c>GameTexts</c>. PlayMode tests check these strings literally.
    /// </summary>
    internal static class WorkshopTexts
    {
        // F-003 pause menu (walking, Esc): 계속 / 새로 시작 — 새로 시작은 확인 후 NewRun.
        public const string PauseTitle = "일시정지";
        public const string Continue = "계속";
        public const string NewRun = "새로 시작";
        public const string PauseKeys = "Esc = 계속, Enter = 새로 시작";

        // F-003 new-run confirm.
        public const string ConfirmNewRunQuestion = "새로 시작할까요?";
        public const string Yes = "예";
        public const string No = "아니오";
        public const string YesNoKeys = "Enter = 예, Esc = 아니오";

        // F-003 sleep confirm (bed): Enter = 예, Esc = 아니오.
        public const string SleepTitle = "잠자기 확인";

        // F-003 settlement: Enter / button → next day; F-012 result: "새로 시작" (Enter).
        public const string NextDay = "다음 날";
        public const string ShowResult = "결과 보기";

        // Counter / bookshelf.
        public const string Close = "닫기";

        // F-013 seated buttons.
        public const string TakeSheet = "종이 꺼내기";
        public const string Finish = "완성";
        public const string Discard = "버리기";
        public const string Undo = "되돌리기";
        public const string Unfold = "처음으로";
        public const string Flip = "뒤집기";
        public const string Book = "책";
        public const string StandUp = "일어나기";
        public const string SkipGuide = "가이드 끄기";
    }
}
