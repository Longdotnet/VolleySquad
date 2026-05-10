namespace VolleySquad.Api.Contracts
{
    public record MatchFinishedEvent
    {
        public Guid MatchId { get; init; }
        public Guid WinningTeamId { get; init; }
        // Thêm các thông tin cần thiết khác
    }
}
