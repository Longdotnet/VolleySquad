namespace VolleySquad.Api.Contracts
{
    // Event được publish khi Admin chốt tiền sân một trận đấu.
    // Payment.Worker sẽ consume event này để trừ Balance mỗi thành viên.
    public record MatchFinalizedEvent
    {
        public Guid MatchId { get; init; }
        public decimal TotalCourtFee { get; init; }
        // Danh sách thành viên đã đăng ký slot — mỗi người trả đều nhau
        public List<Guid> RegisteredMemberIds { get; init; } = new();
    }
}
