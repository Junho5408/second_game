namespace Origami.Core.Flow
{
    /// <summary>Presentation screens of F-007 (screen state only; rules stay in <c>OrigamiRun</c>).</summary>
    public enum FlowScreen
    {
        /// <summary>작업대: day/money/attempts/popular, model list, today's pieces, "하루 마치기".</summary>
        Workbench = 0,
        /// <summary>도안 미리보기 of <see cref="OrigamiFlow.SelectedModelId"/>: full sequence, "접기 시작", "닫기".</summary>
        Preview = 1,
        /// <summary>접기 화면.</summary>
        Folding = 2,
        /// <summary>포기 확인 창 over the folding screen.</summary>
        AbandonConfirm = 3,
        /// <summary>완성/망침/포기 알림.</summary>
        Notice = 4,
        /// <summary>정산 화면 of <see cref="OrigamiFlow.Settlement"/>.</summary>
        Settlement = 5,
        /// <summary>결과 화면 (ending + records, "다시 시작").</summary>
        Result = 6,
    }
}
