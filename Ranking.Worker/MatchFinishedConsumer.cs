using MassTransit;
using Microsoft.EntityFrameworkCore;
using VolleySquad.Api.Contracts;
using VolleySquad.Api.Infrastructure;

namespace Ranking.Worker;

// ============================================================
// RANKING LOGIC - Thuật toán cập nhật SkillPoint sau trận
// ============================================================
// Consumer này xử lý MatchFinishedEvent được publish từ VolleySquad.Api.
//
// WORKER SERVICE LIFETIME:
//   Ranking.Worker là một hosted service chạy vòng lặp liên tục.
//   MassTransit tạo 1 Consumer instance cho MỖI message (Transient-like).
//   AppDbContext được inject qua DI — Scoped trong hosted service context.
//
// IDEMPOTENCY - Xử lý duplicate message:
//   RabbitMQ đảm bảo "at-least-once delivery" — có thể gửi 1 message nhiều lần.
//   Nếu Consumer crash giữa chừng → RabbitMQ retry → SkillPoint bị cộng/trừ 2 lần!
//   Giải pháp production: Lưu MatchId đã xử lý vào DB, kiểm tra trước khi process.
//   Hiện tại chưa implement để giữ code đơn giản (demo purpose).
//
// Áp dụng hệ thống điểm đơn giản dựa trên kết quả trận:
//   - Đội thắng: Mỗi thành viên +5 điểm (tối đa 100)
//   - Đội thua:  Mỗi thành viên -3 điểm (tối thiểu 1)
// Công thức có thể nâng cấp lên Elo/Glicko-2 trong tương lai.
// ============================================================
public class MatchFinishedConsumer : IConsumer<MatchFinishedEvent>
{
    private const int WinPoints = 5;
    private const int LosePoints = 3;

    private readonly ILogger<MatchFinishedConsumer> _logger;
    private readonly AppDbContext _context;

    public MatchFinishedConsumer(ILogger<MatchFinishedConsumer> logger, AppDbContext context)
    {
        _logger = logger;
        _context = context;
    }

    public async Task Consume(ConsumeContext<MatchFinishedEvent> context)
    {
        var evt = context.Message;
        _logger.LogInformation(
            "Processing MatchFinishedEvent: MatchId={MatchId}, Winners={WinnerCount}, Losers={LoserCount}",
            evt.MatchId, evt.WinningTeamMemberIds.Count, evt.LosingTeamMemberIds.Count);

        var allMemberIds = evt.WinningTeamMemberIds.Concat(evt.LosingTeamMemberIds).Distinct().ToList();
        if (allMemberIds.Count == 0)
        {
            _logger.LogWarning("MatchFinishedEvent for MatchId={MatchId} has no member IDs. Skipping.", evt.MatchId);
            return;
        }

        // WHERE Id IN (@ids): 1 query để lấy tất cả members thay vì N query riêng lẻ.
        // Tránh N+1 Problem: KHÔNG gọi FindAsync() trong vòng foreach.
        var members = await _context.Members
            .Where(m => allMemberIds.Contains(m.Id))
            .ToListAsync(context.CancellationToken);

        var winnerSet = new HashSet<Guid>(evt.WinningTeamMemberIds);

        foreach (var member in members)
        {
            var oldSkill = member.SkillPoint;
            bool isWinner = winnerSet.Contains(member.Id);

            // Dùng domain method thay vì tính toán trực tiếp.
            // Lợi ích: Math.Clamp([1,100]) được đảm bảo trong domain — không cần ở đây.
            // Single source of truth: Thay đổi rule ranking → chỉ sửa Member.ApplyMatchResult().
            member.ApplyMatchResult(isWinner, WinPoints, LosePoints);

            _logger.LogDebug(
                "Member {MemberId} ({Name}): {Old} → {New} ({Result})",
                member.Id, member.Name, oldSkill, member.SkillPoint, isWinner ? "WIN +" + WinPoints : "LOSE -" + LosePoints);
        }

        // SaveChangesAsync: EF Core gom tất cả UPDATE thành 1 batch (Unit of Work).
        // Gửi 1 lần thay vì N lần SaveChanges trong loop.
        await _context.SaveChangesAsync(context.CancellationToken);

        _logger.LogInformation(
            "SkillPoint updated for {Count} members in MatchId={MatchId}.",
            members.Count, evt.MatchId);
    }
}
