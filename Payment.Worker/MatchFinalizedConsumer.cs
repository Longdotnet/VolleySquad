using MassTransit;
using Microsoft.EntityFrameworkCore;
using VolleySquad.Api.Contracts;
using VolleySquad.Api.Infrastructure;

namespace Payment.Worker;

// ============================================================
// PAYMENT LOGIC - Xử lý trừ tiền sân sau khi Admin chốt
// ============================================================
// Khi nhận MatchFinalizedEvent:
//   1. Tính FeePerPerson = TotalCourtFee / số thành viên
//   2. Trừ Balance của từng thành viên đã đăng ký
//   3. Nếu Balance âm: ghi log cảnh báo (có thể mở rộng thành thông báo)
// ============================================================
public class MatchFinalizedConsumer : IConsumer<MatchFinalizedEvent>
{
    private readonly ILogger<MatchFinalizedConsumer> _logger;
    private readonly AppDbContext _context;

    public MatchFinalizedConsumer(ILogger<MatchFinalizedConsumer> logger, AppDbContext context)
    {
        _logger = logger;
        _context = context;
    }

    public async Task Consume(ConsumeContext<MatchFinalizedEvent> context)
    {
        var evt = context.Message;
        _logger.LogInformation(
            "Processing MatchFinalizedEvent: MatchId={MatchId}, TotalFee={TotalFee}, Members={Count}",
            evt.MatchId, evt.TotalCourtFee, evt.RegisteredMemberIds.Count);

        if (evt.RegisteredMemberIds.Count == 0)
        {
            _logger.LogWarning("MatchFinalizedEvent for MatchId={MatchId} has no members. Skipping.", evt.MatchId);
            return;
        }

        var feePerPerson = evt.TotalCourtFee / evt.RegisteredMemberIds.Count;

        var members = await _context.Members
            .Where(m => evt.RegisteredMemberIds.Contains(m.Id))
            .ToListAsync(context.CancellationToken);

        foreach (var member in members)
        {
            member.Balance -= feePerPerson;

            if (member.Balance < 0)
            {
                _logger.LogWarning(
                    "Member {MemberId} ({Name}) has insufficient balance after deduction. Balance={Balance}",
                    member.Id, member.Name, member.Balance);
            }
            else
            {
                _logger.LogDebug(
                    "Deducted {Fee} from Member {MemberId} ({Name}). New balance: {Balance}",
                    feePerPerson, member.Id, member.Name, member.Balance);
            }
        }

        await _context.SaveChangesAsync(context.CancellationToken);

        _logger.LogInformation(
            "Fee deduction completed for MatchId={MatchId}. {Count} members charged {Fee} each.",
            evt.MatchId, members.Count, feePerPerson);
    }
}
