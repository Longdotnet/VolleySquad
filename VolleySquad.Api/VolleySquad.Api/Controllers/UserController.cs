// ============================================================
// USER CONTROLLER - Quản lý tài khoản, số dư, và quyết toán tiền sân
// ============================================================
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using MassTransit;
using VolleySquad.Api.Contracts;
using VolleySquad.Api.Domain.Exceptions;
using VolleySquad.Api.Infrastructure;

namespace VolleySquad.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IPublishEndpoint _publishEndpoint;

        // Expression-body constructor: ngắn gọn khi chỉ assign 1 field
        public UserController(AppDbContext context, IPublishEndpoint publishEndpoint)
        {
            _context = context;
            _publishEndpoint = publishEndpoint;
        }

        // ============================================================
        // POST /api/user/deposit - Nạp tiền vào tài khoản
        // ============================================================
        // FIX: [Authorize] => [Authorize(Roles = "Admin")]
        // BUG CŨ: BẤT KỲ member nào cũng gọi được endpoint này với memberId tùy ý.
        //         User tự nạp tiền không giới hạn cho chính mình hoặc người khác!
        //         Đây là lỗ hổng nghiêm trọng về logic nghiệp vụ.
        // FIX: Chỉ Admin mới có quyền nạp tiền (giống như ngân hàng chỉ nhân viên mới nạp tiền).
        [HttpPost("deposit")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Deposit(Guid memberId, decimal amount)
        {
            // Validate input tại biên giới (boundary validation)
            if (amount <= 0) return BadRequest("Số tiền nạp phải lớn hơn 0");

            // FindAsync: Hiệu quả nhất khi tìm theo Primary Key (Guid Id)
            var member = await _context.Members.FindAsync(memberId);
            if (member == null) return NotFound("Không tìm thấy người dùng");

            member.Deposit(amount);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                Message = $"Đã nạp {amount:N0} VNĐ thành công",
                Name = member.Name,
                NewBalance = member.Balance
            });
        }

        // ============================================================
        // GET /api/user/my-balance - Xem số dư của chính mình
        // ============================================================
        // FIX: Tìm member theo Id (từ claim) thay vì theo Name.
        // BUG CŨ: m.Name == userName => nếu 2 người trùng tên sẽ lấy người đầu tiên.
        //         Tên không phải định danh duy nhất — Id (Guid) mới là ID duy nhất.
        //
        // Quy trình:
        //   1. User login → AuthController gắn member.Id vào claim NameIdentifier
        //   2. User gọi /my-balance → ta đọc claim NameIdentifier → tìm đúng member trong DB
        [HttpGet("my-balance")]
        [Authorize]
        public async Task<IActionResult> GetBalance()
        {
            // User là ClaimsPrincipal được ASP.NET Core tạo sau khi giải mã JWT.
            // FindFirst: Trả về claim đầu tiên khớp type, hoặc null nếu không có.
            // ?.Value: Null-conditional operator — tránh NullReferenceException.
            var memberIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(memberIdStr, out var memberId))
                return Unauthorized("Token không hợp lệ");

            // FIX: Tìm theo Id thay vì Name => chính xác, không bị nhầm khi trùng tên
            var member = await _context.Members.FindAsync(memberId);
            if (member == null) return NotFound("Không tìm thấy thông tin tài khoản");

            return Ok(new { Name = member.Name, Balance = member.Balance });
        }

        // ============================================================
        // POST /api/user/finalize-match/{matchId} - Admin chốt tiền sân
        // ============================================================
        // Quy trình: Tính phí/người → kiểm tra đủ tiền → trừ tiền tất cả (trong Transaction)
        [HttpPost("finalize-match/{matchId}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> FinalizeMatch(Guid matchId, decimal totalCourtFee)
        {
            if (totalCourtFee <= 0) return BadRequest("Tiền sân phải lớn hơn 0");

            var match = await _context.Matches.FindAsync(matchId);
            if (match == null) return NotFound("Trận đấu không tồn tại");

            var participantCount = match.RegisteredMemberIds.Count;
            if (participantCount == 0) return BadRequest("Không có ai đăng ký để chia tiền");

            decimal feePerPerson = totalCourtFee / participantCount;

            // Load tất cả member tham gia trong 1 query (tránh N+1 query problem)
            // N+1 problem: Vòng lặp foreach gọi FindAsync mỗi lần = N query riêng lẻ => chậm.
            // FIX: 1 query WHERE Id IN (...) lấy tất cả về rồi xử lý trong memory.
            var participants = await _context.Members
                .Where(m => match.RegisteredMemberIds.Contains(m.Id))
                .ToListAsync();

            // FIX: Kiểm tra tất cả thành viên có đủ tiền TRƯỚC khi trừ.
            // BUG CŨ: Cho phép balance âm âm thầm — user không biết mình nợ.
            // FIX: Fail fast — báo lỗi sớm thay vì để tiền âm.
            var insufficientMembers = participants.Where(m => m.Balance < feePerPerson).ToList();
            if (insufficientMembers.Any())
            {
                var names = string.Join(", ", insufficientMembers.Select(m => m.Name));
                return BadRequest($"Các thành viên sau không đủ số dư: {names}. " +
                                  $"Mỗi người cần {feePerPerson:N0} VNĐ");
            }

            try
            {
                // Backward-compat endpoint: giữ route cũ nhưng đưa về cùng flow event-driven
                // với /api/match/finalize để tránh 2 nơi xử lý tiền theo 2 kiểu khác nhau.
                match.Settle(totalCourtFee);

                await _context.SaveChangesAsync();

                await _publishEndpoint.Publish(new MatchFinalizedEvent
                {
                    MatchId = matchId,
                    TotalCourtFee = totalCourtFee,
                    RegisteredMemberIds = match.RegisteredMemberIds
                });

                return Ok(new
                {
                    Message = "Đã chốt trận thành công. Payment.Worker đang xử lý.",
                    TotalFee = totalCourtFee,
                    FeePerPerson = feePerPerson,
                    ParticipantCount = participantCount
                });
            }
            catch (DomainException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception)
            {
                return StatusCode(500, "Lỗi khi xử lý thanh toán, vui lòng thử lại");
            }
        }
    }
}
