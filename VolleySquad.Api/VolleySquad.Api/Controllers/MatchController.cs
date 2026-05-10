// ============================================================
// MATCH CONTROLLER - Quản lý trận đấu và đăng ký slot
// ============================================================
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using VolleySquad.Api.Domain;
using VolleySquad.Api.Infrastructure;
using VolleySquad.Api.Services;
using MassTransit;
using VolleySquad.Api.Contracts;

namespace VolleySquad.Api.Controllers
{
    // [Route("api/[controller]")]: URL = /api/match (bỏ chữ "Controller")
    // [ApiController]: Tự động trả 400 nếu model invalid, auto-bind [FromBody]/[FromRoute]
    [Route("api/[controller]")]
    [ApiController]
    public class MatchController : ControllerBase
    {
        // readonly: field không bị reassign sau constructor => an toàn hơn trong môi trường concurrent
        private readonly TeamService _teamService;
        private readonly AppDbContext _context;
        private readonly IPublishEndpoint _publishEndpoint;

        // DI Container tự inject TeamService và AppDbContext khi request đến.
        // Không cần new() thủ công => dễ unit test (có thể truyền mock object vào).
        public MatchController(TeamService teamService, AppDbContext context, IPublishEndpoint publishEndpoint)
        {
            _teamService = teamService;
            _context = context;
            _publishEndpoint = publishEndpoint;
        }

        // ============================================================
        // GET /api/match/members - Lấy danh sách thành viên (Admin only)
        // ============================================================
        // [Authorize(Roles = "Admin")]: Chỉ token có claim Role="Admin" mới được phép.
        // UseAuthorization middleware đọc ClaimsPrincipal.IsInRole("Admin") để quyết định.
        [HttpGet("members")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetMembers()
        {
            // ToListAsync(): Gửi "SELECT * FROM Members" lên SQL Server.
            // await giải phóng thread về ThreadPool trong lúc chờ DB.
            // Không dùng .ToList() (blocking) vì thread bị đóng băng, lãng phí resource.
            var members = await _context.Members.ToListAsync();
            return Ok(members);
        }

        // ============================================================
        // POST /api/match/add-member - Thêm thành viên mới (Admin only)
        // ============================================================
        // FIX: [Authorize] (mọi user) => [Authorize(Roles = "Admin")]
        // BUG CŨ: Member thường gọi được endpoint này, tự tạo tài khoản với Balance,
        //         SkillPoint tùy ý, thậm chí tự set Role = "Admin" cho chính mình!
        [HttpPost("add-member")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AddMember([FromBody] Member member)
        {
            // Server tự tạo Id, không tin vào Id từ client (tránh dùng Id trùng hoặc giả)
            member.Id = Guid.NewGuid();

            // Whitelist Role: chỉ cho phép "Admin" hoặc "Member", chặn giá trị lạ
            if (member.Role != "Admin" && member.Role != "Member")
                member.Role = "Member";

            _context.Members.Add(member);
            // SaveChangesAsync(): EF Core gom tất cả thay đổi trong DbContext và
            // gửi 1 batch INSERT lên DB (Unit of Work pattern).
            await _context.SaveChangesAsync();

            // 201 Created đúng chuẩn REST hơn 200 OK cho request tạo resource mới.
            return CreatedAtAction(nameof(GetMembers), new { id = member.Id }, member);
        }

        // ============================================================
        // POST /api/match/split-from-db - Chia 3 đội bằng thuật toán Snake Draft
        // ============================================================
        [HttpPost("split-from-db")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> SplitFromDb()
        {
            var players = await _context.Members.ToListAsync();
            if (players.Count < 3) return BadRequest("Không đủ người để chia 3 đội (cần ít nhất 3 người)!");

            var result = _teamService.BalanceTeams(players);
            return Ok(new { TeamA = result[0], TeamB = result[1], TeamC = result[2] });
        }

        // ============================================================
        // POST /api/match/register-slot/{matchId} - Member tự đăng ký tham gia
        // ============================================================
        // FIX: Bỏ tham số memberId ra khỏi request.
        // BUG CŨ: Client truyền memberId bất kỳ => user A đăng ký hộ user B,
        //         hoặc tấn công spam đăng ký tất cả slot bằng ID giả.
        // FIX: Lấy memberId từ JWT Claims => chỉ đăng ký được cho chính mình.
        [HttpPost("register-slot/{matchId}")]
        [Authorize]
        public async Task<IActionResult> RegisterSlot(Guid matchId)
        {
            // ============================================================
            // ĐỌC THÔNG TIN TỪ JWT CLAIMS
            // ============================================================
            // Sau UseAuthentication() middleware chạy, JWT được giải mã và gán vào
            // User (ClaimsPrincipal) — accessible ở mọi Controller qua thuộc tính User.
            // ClaimTypes.NameIdentifier = claim "nameid" = member.Id.ToString() từ lúc login.
            var memberIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(memberIdStr, out var memberId))
                return Unauthorized("Token không hợp lệ hoặc thiếu thông tin định danh");

            // ============================================================
            // DATABASE TRANSACTION - Tính nguyên tử (Atomicity)
            // ============================================================
            // ACID: Atomicity, Consistency, Isolation, Durability
            //   Atomicity: Tất cả thành công HOẶC tất cả thất bại (không có trạng thái lửng)
            //   Isolation: Các transaction song song không ảnh hưởng nhau
            //              => tránh 2 người cùng đăng ký slot cuối cùng cùng lúc
            //
            // "using var": [IDisposable pattern] Tự gọi Dispose() khi ra khỏi scope.
            // Nếu không CommitAsync(), transaction tự RollbackAsync() khi Dispose.
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // FindAsync: Tìm theo Primary Key, nhanh hơn FirstOrDefaultAsync
                // vì EF Core có thể dùng identity map cache trong cùng DbContext scope.
                var match = await _context.Matches.FindAsync(matchId);
                if (match == null) return NotFound("Không tìm thấy trận đấu");

                if (match.RegisteredMemberIds.Count >= match.MaxSlots)
                    return BadRequest("Sân đã đầy slot!");

                if (match.RegisteredMemberIds.Contains(memberId))
                    return BadRequest("Bạn đã đăng ký trận này rồi");

                // AnyAsync: Chỉ kiểm tra tồn tại (SELECT TOP 1), hiệu quả hơn FindAsync
                // khi chỉ cần biết có tồn tại hay không mà không cần load object.
                var memberExists = await _context.Members.AnyAsync(m => m.Id == memberId);
                if (!memberExists)
                    return NotFound("Không tìm thấy thông tin thành viên");

                match.RegisteredMemberIds.Add(memberId);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync(); // Ghi vĩnh viễn vào DB

                return Ok("Đăng ký thành công!");
            }
            catch (Exception)
            {
                await transaction.RollbackAsync(); // Hoàn tác về trạng thái trước transaction
                return StatusCode(500, "Có lỗi xảy ra khi đăng ký, vui lòng thử lại");
            }
        }
        // ============================================================
        // GET /api/match/all-matches - Danh sách trận đấu (mọi user đã login)
        // ============================================================
        [HttpGet("all-matches")]
        [Authorize]
        public async Task<IActionResult> GetAllMatches()
        {
            var matches = await _context.Matches.ToListAsync();
            return Ok(matches);
        }

        // ============================================================
        // GET /api/match/{id} - Chi tiết 1 trận
        // ============================================================
        // {id} trong route template => ASP.NET Core auto-bind vào tham số Guid id ([FromRoute])
        [HttpGet("{id}")]
        [Authorize]
        public async Task<IActionResult> GetMatchById(Guid id)
        {
            var match = await _context.Matches.FindAsync(id);
            if (match == null) return NotFound("Không tìm thấy trận đấu");
            return Ok(match);
        }

        // ============================================================
        // POST /api/match/create-match - Admin tạo trận mới
        // ============================================================
        // [FromBody]: Đọc JSON từ Request Body và deserialize thành Match object.
        // [ApiController] tự xử lý việc này, không cần gọi thủ công.
        [HttpPost("create-match")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateMatch([FromBody] Match match)
        {
            match.Id = Guid.NewGuid(); // Server tự tạo Id, không tin vào Id từ client
            if (match.MaxSlots <= 0) match.MaxSlots = 18;

            _context.Matches.Add(match);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetMatchById), new { id = match.Id }, match);
        }

        // ============================================================
        // PUT /api/match/update-match/{id} - Cập nhật trận đấu
        // ============================================================
        // PUT vs PATCH:
        //   PUT:   Thay toàn bộ resource (idempotent — gọi nhiều lần = kết quả như nhau)
        //   PATCH: Thay một phần resource (linh hoạt hơn, phức tạp hơn với JsonPatch)
        [HttpPut("update-match/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateMatch(Guid id, [FromBody] Match updatedMatch)
        {
            var existingMatch = await _context.Matches.FindAsync(id);
            if (existingMatch == null) return NotFound("Trận đấu không tồn tại");

            // Chỉ update field được phép, giữ nguyên Id và RegisteredMemberIds
            existingMatch.Location = updatedMatch.Location;
            existingMatch.PlayDate = updatedMatch.PlayDate;
            existingMatch.MaxSlots = updatedMatch.MaxSlots;

            await _context.SaveChangesAsync();
            return Ok(existingMatch);
        }

        // ============================================================
        // DELETE /api/match/delete-match/{id} - Xóa trận đấu
        // ============================================================
        // 204 No Content: Xóa thành công, không có data trả về (đúng chuẩn REST)
        [HttpDelete("delete-match/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteMatch(Guid id)
        {
            var match = await _context.Matches.FindAsync(id);
            if (match == null) return NotFound("Không tìm thấy trận để xóa");

            _context.Matches.Remove(match);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // ============================================================
        // POST /api/match/pass-slot/{matchId} - Tạo yêu cầu nhượng slot
        // ============================================================
        // Người gọi (fromMember) phải đang có slot trong trận.
        // toMemberId: Người được nhượng (chưa có slot trong trận).
        // Kết quả: Tạo SlotTransfer với Status = "Pending" — chờ toMember xác nhận.
        //
        // Rule quan trọng:
        //   - Không cho phép nhượng nếu trận đã chốt tiền (IsSettled) mà toMember không đủ tiền.
        //   - Mỗi slot chỉ có 1 yêu cầu nhượng Pending tại một thời điểm.
        [HttpPost("pass-slot/{matchId}")]
        [Authorize]
        public async Task<IActionResult> PassSlot(Guid matchId, [FromQuery] Guid toMemberId)
        {
            var fromMemberIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(fromMemberIdStr, out var fromMemberId))
                return Unauthorized("Token không hợp lệ hoặc thiếu thông tin định danh");

            if (fromMemberId == toMemberId)
                return BadRequest("Không thể nhượng slot cho chính mình");

            var match = await _context.Matches.FindAsync(matchId);
            if (match == null) return NotFound("Không tìm thấy trận đấu");

            if (!match.RegisteredMemberIds.Contains(fromMemberId))
                return BadRequest("Bạn không có slot trong trận đấu này");

            if (match.RegisteredMemberIds.Contains(toMemberId))
                return BadRequest("Người nhận đã có slot trong trận đấu này");

            var toMemberExists = await _context.Members.AnyAsync(m => m.Id == toMemberId);
            if (!toMemberExists) return NotFound("Không tìm thấy thành viên nhận slot");

            // Kiểm tra nếu trận đã chốt thì người nhận phải đủ tiền
            if (match.IsSettled)
            {
                var toMember = await _context.Members.FindAsync(toMemberId);
                if (toMember!.Balance < match.FeePerPerson)
                    return BadRequest($"Người nhận không đủ số dư. " +
                                      $"Cần {match.FeePerPerson:N0} VNĐ, hiện có {toMember.Balance:N0} VNĐ");
            }

            // Kiểm tra đã có yêu cầu Pending cho slot này chưa
            var existingPending = await _context.SlotTransfers.AnyAsync(t =>
                t.MatchId == matchId &&
                t.FromMemberId == fromMemberId &&
                t.Status == "Pending");

            if (existingPending)
                return BadRequest("Bạn đang có yêu cầu nhượng slot đang chờ xử lý. Hãy huỷ trước khi tạo mới");

            var transfer = new SlotTransfer
            {
                Id = Guid.NewGuid(),
                MatchId = matchId,
                FromMemberId = fromMemberId,
                ToMemberId = toMemberId,
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            _context.SlotTransfers.Add(transfer);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                Message = "Yêu cầu nhượng slot đã được tạo, chờ người nhận xác nhận",
                TransferId = transfer.Id,
                MatchId = matchId,
                ToMemberId = toMemberId
            });
        }

        // ============================================================
        // POST /api/match/accept-slot/{transferId} - Xác nhận nhận slot
        // ============================================================
        // Chỉ toMember (người được nhượng) mới có thể gọi endpoint này.
        // Atomic transaction:
        //   1. Xóa fromMember khỏi RegisteredMemberIds
        //   2. Thêm toMember vào RegisteredMemberIds
        //   3. Nếu IsSettled: hoàn FeePerPerson cho fromMember, trừ FeePerPerson của toMember
        //   4. Đánh dấu SlotTransfer.Status = "Completed"
        [HttpPost("accept-slot/{transferId}")]
        [Authorize]
        public async Task<IActionResult> AcceptSlot(Guid transferId)
        {
            var memberIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(memberIdStr, out var memberId))
                return Unauthorized("Token không hợp lệ hoặc thiếu thông tin định danh");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var transfer = await _context.SlotTransfers.FindAsync(transferId);
                if (transfer == null) return NotFound("Không tìm thấy yêu cầu nhượng slot");
                if (transfer.ToMemberId != memberId)
                    return Forbid(); // Chỉ người được chỉ định mới được xác nhận
                if (transfer.Status != "Pending")
                    return BadRequest($"Yêu cầu này đã ở trạng thái '{transfer.Status}', không thể xác nhận");

                var match = await _context.Matches.FindAsync(transfer.MatchId);
                if (match == null) return NotFound("Không tìm thấy trận đấu liên quan");

                // Kiểm tra lại tính hợp lệ tại thời điểm accept (tránh race condition)
                if (!match.RegisteredMemberIds.Contains(transfer.FromMemberId))
                    return BadRequest("Người nhượng không còn slot trong trận này");

                if (match.RegisteredMemberIds.Contains(transfer.ToMemberId))
                    return BadRequest("Bạn đã có slot trong trận này rồi");

                var fromMember = await _context.Members.FindAsync(transfer.FromMemberId);
                var toMember = await _context.Members.FindAsync(transfer.ToMemberId);
                if (fromMember == null || toMember == null)
                    return NotFound("Không tìm thấy thông tin thành viên");

                // Xử lý tiền nếu trận đã chốt (IsSettled)
                if (match.IsSettled)
                {
                    if (toMember.Balance < match.FeePerPerson)
                        return BadRequest($"Số dư không đủ. " +
                                          $"Cần {match.FeePerPerson:N0} VNĐ, hiện có {toMember.Balance:N0} VNĐ");

                    fromMember.Balance += match.FeePerPerson; // Hoàn tiền cho người nhượng
                    toMember.Balance -= match.FeePerPerson;   // Trừ tiền người nhận
                }

                // Chuyển slot
                match.RegisteredMemberIds.Remove(transfer.FromMemberId);
                match.RegisteredMemberIds.Add(transfer.ToMemberId);

                // Hoàn tất yêu cầu
                transfer.Status = "Completed";
                transfer.ResolvedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new
                {
                    Message = "Nhượng slot thành công!",
                    MatchId = match.Id,
                    From = fromMember.Name,
                    To = toMember.Name,
                    FeeRefunded = match.IsSettled ? match.FeePerPerson : 0,
                    FeeCharged = match.IsSettled ? match.FeePerPerson : 0
                });
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, "Có lỗi xảy ra khi xử lý nhượng slot, vui lòng thử lại");
            }
        }

        // ============================================================
        // POST /api/match/cancel-slot-transfer/{transferId} - Huỷ yêu cầu nhượng slot
        // ============================================================
        // Người nhượng (fromMember) hoặc Admin có thể huỷ yêu cầu Pending.
        [HttpPost("cancel-slot-transfer/{transferId}")]
        [Authorize]
        public async Task<IActionResult> CancelSlotTransfer(Guid transferId)
        {
            var memberIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(memberIdStr, out var memberId))
                return Unauthorized("Token không hợp lệ hoặc thiếu thông tin định danh");

            var transfer = await _context.SlotTransfers.FindAsync(transferId);
            if (transfer == null) return NotFound("Không tìm thấy yêu cầu nhượng slot");
            if (transfer.Status != "Pending")
                return BadRequest($"Yêu cầu đã ở trạng thái '{transfer.Status}', không thể huỷ");

            // Chỉ người nhượng hoặc Admin mới được huỷ
            var isAdmin = User.IsInRole("Admin");
            if (transfer.FromMemberId != memberId && !isAdmin)
                return Forbid();

            transfer.Status = "Cancelled";
            transfer.ResolvedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new { Message = "Đã huỷ yêu cầu nhượng slot", TransferId = transferId });
        }

        // ============================================================
        // GET /api/match/slot-transfers/{matchId} - Xem yêu cầu nhượng slot của trận
        // ============================================================
        [HttpGet("slot-transfers/{matchId}")]
        [Authorize]
        public async Task<IActionResult> GetSlotTransfers(Guid matchId)
        {
            var memberIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(memberIdStr, out var memberId))
                return Unauthorized("Token không hợp lệ hoặc thiếu thông tin định danh");

            var isAdmin = User.IsInRole("Admin");

            // Member chỉ thấy các transfer liên quan đến mình; Admin thấy tất cả
            var query = _context.SlotTransfers.Where(t => t.MatchId == matchId);
            if (!isAdmin)
                query = query.Where(t => t.FromMemberId == memberId || t.ToMemberId == memberId);

            var transfers = await query
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            return Ok(transfers);
        }

        // ============================================================
        // POST /api/match/finish - Admin chốt kết quả trận đấu
        // ============================================================
        [HttpPost("finish")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> FinishMatch([FromBody] FinishMatchRequest request)
        {
            var match = await _context.Matches.FindAsync(request.MatchId);
            if (match == null) return NotFound("Không tìm thấy trận đấu");

            // TODO: Thêm logic xác thực `WinningTeamId` hợp lệ

            // Bắn event MatchFinishedEvent lên RabbitMQ
            await _publishEndpoint.Publish(new MatchFinishedEvent
            {
                MatchId = request.MatchId,
                WinningTeamId = request.WinningTeamId
            });

            return Accepted("Đã tiếp nhận yêu cầu xử lý kết quả trận đấu.");
        }

        public record FinishMatchRequest(Guid MatchId, Guid WinningTeamId);
    }
}