namespace Majungbul
{
    /// <summary>GDD「게임 규칙과 상태」의 여섯 상태 + 날짜 전환 연출용 Transition.</summary>
    public enum GamePhase
    {
        Title,      // 시작 화면
        Return,     // 귀가 (첫날)
        Transition, // 날짜 전환 자막
        Anomaly,    // 이상 징후 (사흘째 저녁)
        Farewell,   // 송별
        Success,    // 성공 (귀환)
        Failure     // 실패 (남겨짐)
    }

    public enum PropKind
    {
        Cup,
        Photo,
        WindChime,
        WatcherHand
    }
}
