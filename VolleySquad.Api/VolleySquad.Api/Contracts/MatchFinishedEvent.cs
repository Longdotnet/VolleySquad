namespace VolleySquad.Api.Contracts
{
    public record MatchFinishedEvent
    {
        public Guid MatchId { get; init; }
        public Guid WinningTeamId { get; init; }
        // Danh sách thành viên đội thắng và đội thua để Ranking.Worker tính điểm
        public List<Guid> WinningTeamMemberIds { get; init; } = new();
        public List<Guid> LosingTeamMemberIds { get; init; } = new();
    }
}
