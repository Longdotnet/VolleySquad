using MassTransit;
using VolleySquad.Api.Contracts;

namespace Ranking.Worker;

public class MatchFinishedConsumer : IConsumer<MatchFinishedEvent>
{
    private readonly ILogger<MatchFinishedConsumer> _logger;

    public MatchFinishedConsumer(ILogger<MatchFinishedConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<MatchFinishedEvent> context)
    {
        _logger.LogInformation("Received MatchFinishedEvent: MatchId={MatchId}, WinningTeamId={WinningTeamId}", 
            context.Message.MatchId, context.Message.WinningTeamId);
        
        // TODO: Thêm logic xử lý cập nhật SkillPoint ở đây
        // 1. Lấy thông tin các thành viên trong trận đấu từ DB
        // 2. Xác định đội thắng, đội thua
        // 3. Áp dụng thuật toán Elo/Glicko-2 để tính toán lại SkillPoint
        // 4. Cập nhật SkillPoint mới vào DB

        return Task.CompletedTask;
    }
}
