// ============================================================
// PUBLIC CONTROLLER - Endpoint không cần xác thực
// ============================================================
// Mục đích: Cung cấp số liệu thống kê và nhật ký hoạt động
//   cho trang Login (bên trái panel) — không yêu cầu JWT.
//
// SECURITY NOTE:
//   Không có [Authorize] → Bất kỳ ai cũng có thể gọi.
//   Chỉ trả về dữ liệu TỔNG HỢP (count, max) và log tin nhắn.
//   KHÔNG trả về thông tin cá nhân nhạy cảm (email, password, balance...).
//   Đây là dữ liệu "marketing" — hiển thị sức sống cộng đồng.
//
// RATE LIMITING:
//   Endpoint này được bảo vệ bởi rate limiter mặc định của app.
//   Nếu cần, có thể thêm [EnableRateLimiting("PublicPolicy")] riêng.
//
// CACHING:
//   Stats không thay đổi quá nhanh → Cache 60 giây là hợp lý.
//   Dùng [ResponseCache] cho HTTP cache (304 Not Modified, CDN-friendly).
//   TODO (future): Thêm IMemoryCache để tránh DB query mỗi request.
// ============================================================
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VolleySquad.Api.Domain.Enums;
using VolleySquad.Api.Infrastructure;

namespace VolleySquad.Api.Controllers
{
    [Route("api/public")]
    [ApiController]
    public class PublicController(AppDbContext context) : ControllerBase
    {
        // DTO Response: Số liệu thống kê tổng hợp cho trang Login
        public record PublicStatsResponse(
            int MemberCount,
            int MatchesPlayed,
            int HighestSkillPoint,
            int MatchesThisMonth,
            int UpcomingMatches
        );

        // DTO Response: Một mục trong nhật ký hoạt động
        public record PublicActivityResponse(
            string Message,
            DateTime OccurredAt,
            string EventType
        );

        /// <summary>
        /// Lấy thống kê tổng hợp của hệ thống.
        /// Không cần xác thực. Cache 60 giây.
        /// </summary>
        /// <returns>Các chỉ số tổng quan: số thành viên, trận đấu, skill cao nhất, v.v.</returns>
        [HttpGet("stats")]
        [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Any)]
        public async Task<ActionResult<PublicStatsResponse>> GetStats()
        {
            var now = DateTime.UtcNow;

            // Chạy song song các query độc lập để giảm latency tổng
            // (EF Core cho phép nhiều query đồng thời trên cùng 1 context instance
            //  nhưng KHÔNG phải concurrent — đây là sequential await tasks)
            // Thực tế: Chạy từng cái, không parallel vì DbContext không thread-safe.
            // Để parallel thực sự, cần tạo nhiều DbContext instance (dùng IDbContextFactory).
            var memberCount = await context.Members.CountAsync();

            var matchesPlayed = await context.Matches
                .CountAsync(m => m.Status == MatchStatus.Finished || m.Status == MatchStatus.Settled);

            // MaxAsync((int?)...) ?? 0: trả về null khi empty set thay vì throw exception
            // EF Core translate thành SQL MAX() — NULL-safe, không cần DefaultIfEmpty
            var highestSkillPoint = (int)(await context.Members
                .MaxAsync(m => (int?)m.SkillPoint) ?? 0);

            var matchesThisMonth = await context.Matches
                .CountAsync(m =>
                    m.PlayDate.Month == now.Month &&
                    m.PlayDate.Year  == now.Year  &&
                    (m.Status == MatchStatus.Finished || m.Status == MatchStatus.Settled));

            var upcomingMatches = await context.Matches
                .CountAsync(m => m.Status == MatchStatus.Upcoming && m.PlayDate >= now);

            return Ok(new PublicStatsResponse(
                memberCount,
                matchesPlayed,
                highestSkillPoint,
                matchesThisMonth,
                upcomingMatches
            ));
        }

        /// <summary>
        /// Lấy danh sách hoạt động gần nhất (Activity Feed).
        /// Không cần xác thực. Dùng cho ticker/marquee trang Login.
        /// </summary>
        /// <param name="count">Số lượng hoạt động cần lấy (mặc định 20, tối đa 50)</param>
        [HttpGet("activities")]
        [ResponseCache(Duration = 30, Location = ResponseCacheLocation.Any)]
        public async Task<ActionResult<IEnumerable<PublicActivityResponse>>> GetActivities(
            [FromQuery] int count = 20)
        {
            // Giới hạn count để tránh client request quá nhiều
            count = Math.Clamp(count, 1, 50);

            var activities = await context.MatchActivities
                .OrderByDescending(a => a.OccurredAt)  // Mới nhất trước
                .Take(count)
                .Select(a => new PublicActivityResponse(
                    a.Message,
                    a.OccurredAt,
                    a.EventType.ToString() // Enum → string tên (VD: "MemberJoinedMatch")
                ))
                .ToListAsync();

            return Ok(activities);
        }
    }
}
