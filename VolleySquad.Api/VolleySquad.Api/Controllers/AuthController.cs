// ============================================================
// AUTH CONTROLLER - Xử lý đăng nhập và cấp JWT Token
// ============================================================
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.RateLimiting;
using VolleySquad.Api.Infrastructure;
using VolleySquad.Api.Domain;
using VolleySquad.Api.Services.Interfaces;

namespace VolleySquad.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        // ============================================================
        // DEPENDENCY INJECTION - Constructor Injection Pattern
        // ============================================================
        // Thay vì tự tạo: var context = new AppDbContext(...);
        // Ta để DI Container tự inject qua constructor khi request đến.
        // Lợi ích: dễ unit test (có thể mock), giảm coupling, DI container quản lý lifetime.
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IPasswordService _passwordService;

        public AuthController(AppDbContext context, IConfiguration configuration, IPasswordService passwordService)
        {
            _context = context;
            _configuration = configuration;
            _passwordService = passwordService;
        }

        // ============================================================
        // DTO (Data Transfer Object) - Đối tượng truyền dữ liệu
        // ============================================================
        // Dùng record thay vì class vì record immutable (bất biến) — phù hợp với input.
        // KHÔNG dùng trực tiếp entity Member để tránh lộ các field nhạy cảm (Balance, Role...).
        // record trong C# tự generate: constructor, ToString, Equals, GetHashCode.
        public sealed record LoginRequest
        {
            [Required, StringLength(100, MinimumLength = 3)]
            public string Username { get; init; } = string.Empty;

            [Required, StringLength(128, MinimumLength = 8)]
            public string Password { get; init; } = string.Empty;
        }

        public sealed record MemberSummary(Guid Id, string Name, int SkillPoint, decimal Balance, string Role);

        public sealed record LoginResponse(string Token, DateTime Expires, MemberSummary Member);

        public sealed record RegisterRequest
        {
            [Required, StringLength(100, MinimumLength = 2)]
            public string Name { get; init; } = string.Empty;

            [Required, StringLength(128, MinimumLength = 8)]
            public string Password { get; init; } = string.Empty;
        }

        public sealed record ChangePasswordRequest
        {
            [Required, StringLength(128, MinimumLength = 8)]
            public string CurrentPassword { get; init; } = string.Empty;

            [Required, StringLength(128, MinimumLength = 8)]
            public string NewPassword { get; init; } = string.Empty;
        }

        // ============================================================
        // FIX 1: [HttpPost] thay vì [HttpGet]
        // ============================================================
        // GET request lưu URL vào: browser history, server access log, proxy log, Referer header.
        // Credentials KHÔNG ĐƯỢC xuất hiện trong URL — đây là nguyên tắc bảo mật cơ bản.
        // POST gửi data trong Request Body (không lưu trong log mặc định).
        // Thao tác "đăng nhập" là action có side effect => đúng semantic của POST trong RESTful.
        //
        // ============================================================
        // ASYNC/AWAIT - Tại sao không dùng void hay IActionResult thường?
        // ============================================================
        // KHÔNG async (blocking): Thread bị BLOCK trong khi chờ DB trả kết quả.
        //   Request 1 → Thread bị "đóng băng" chờ DB 200ms → không làm được gì khác
        //   Request 2 → Phải xếp hàng chờ thread rảnh → Server tắc nghẽn
        //
        // CÓ async/await (non-blocking I/O):
        //   Request 1 → Thread gọi DB, gặp "await" → NHƯỜNG thread về ThreadPool
        //   Request 2 → Thread vừa rảnh đó xử lý Request 2 ngay lập tức
        //   (Khi DB xong) → Thread bất kỳ trong pool tiếp tục hoàn thành Response 1
        //
        // Task<IActionResult>: Hàm hứa sẽ trả về IActionResult SAU KHI xong việc async.
        //   Giống Promise<Response> trong JavaScript.
        //
        // ============================================================
        // RATE LIMITING - Chống Brute Force (OWASP A07)
        // ============================================================
        // [EnableRateLimiting("LoginPolicy")]: Áp dụng giới hạn 10 req/phút cho endpoint này.
        // Tại sao chỉ giới hạn Login mà không phải tất cả endpoint?
        //   - Endpoint login là mục tiêu của brute force attack (thử hàng nghìn password)
        //   - Các endpoint khác đã được bảo vệ bằng JWT ([Authorize]) nên ít bị tấn công hơn
        //   - Rate limiting tốn thêm memory/CPU nên chỉ áp dụng nơi cần thiết
        [HttpPost("login")]
        [EnableRateLimiting("LoginPolicy")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            // Validate input tại biên giới hệ thống (data đến từ bên ngoài luôn phải kiểm tra)
            if (!ModelState.IsValid)
                return ValidationProblem(ModelState);

            var normalizedUsername = request.Username.Trim();
            var normalizedPassword = request.Password.Trim();

            // ============================================================
            // FIX 2: Tìm member trong DB để lấy Role và Id thật
            // ============================================================
            // BUG CŨ: username == "admin" => admin. Bất kỳ ai đặt tên "admin" thành Admin!
            // FIX: Đọc Role từ DB — chỉ ai được Admin tạo với Role="Admin" mới có quyền Admin.
            //
            // FirstOrDefaultAsync: Trả về phần tử đầu tiên hoặc null nếu không tìm thấy.
            // Luôn dùng bản Async khi query DB để không block thread (xem giải thích async ở trên).
            var candidates = await _context.Members
                .Where(m => m.Name.ToLower() == normalizedUsername.ToLower())
                .Take(2)
                .ToListAsync();

            // Generic error message để tránh username enumeration.
            // Interview question hay gặp: "Tại sao không nói rõ sai username hay password?"
            // -> Vì attacker có thể lợi dụng message chi tiết để dò tài khoản hợp lệ.
            if (candidates.Count != 1)
                return Unauthorized("Thông tin đăng nhập không đúng");

            var member = candidates[0];
            if (!member.HasPasswordConfigured())
                return Unauthorized("Tài khoản này chưa được cấu hình mật khẩu. Liên hệ Admin để reset mật khẩu.");

            if (!_passwordService.VerifyPassword(normalizedPassword, member.PasswordHash!))
                return Unauthorized("Thông tin đăng nhập không đúng");

            // ============================================================
            // JWT CLAIMS - Thông tin được nhúng vào Token
            // ============================================================
            // Claims là các "tuyên bố" về người dùng được đưa vào JWT Payload.
            // Sau khi login, mỗi request mang token này => server KHÔNG cần query DB
            // để biết "ai đang gọi" => STATELESS (khác với Session/Cookie stateful).
            //
            // Cấu trúc JWT: Header.Payload.Signature
            //   Header:    {"alg":"HS256","typ":"JWT"}
            //   Payload:   {"nameid":"<guid>","unique_name":"Lan","role":"Admin","exp":...}
            //   Signature: HMACSHA256(base64(header)+"."+base64(payload), secretKey)
            //
            // FIX 3: Thêm ClaimTypes.NameIdentifier (sub) = member.Id
            // ClaimTypes.NameIdentifier = "nameid" trong JWT => định danh duy nhất của user.
            // Dùng ID (Guid) thay vì tên để tránh nhầm lẫn khi 2 user trùng tên.
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, member.Id.ToString()), // FIX: ID từ DB
                new Claim(ClaimTypes.Name, member.Name),
                new Claim(ClaimTypes.Role, member.Role),                    // FIX: Role từ DB
                new Claim(JwtRegisteredClaimNames.Sub, member.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.UniqueName, member.Name)
            };

            // FIX 4: Đọc SecretKey từ IConfiguration thay vì hardcode
            // Hardcode trong code => lộ key khi push lên GitHub => attacker tạo được token giả
            var issuer = _configuration["JwtSettings:Issuer"]!;
            var audience = _configuration["JwtSettings:Audience"]!;
            var secretKey = _configuration["JwtSettings:SecretKey"]!;
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var expiresAt = DateTime.UtcNow.AddDays(1);

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                // FIX 5: UtcNow thay vì Now
                // JWT spec (RFC 7519) yêu cầu timestamp theo UTC.
                // Nếu dùng Now và server ở múi giờ +7, token có thể bị tính sai giờ hết hạn.
                expires: expiresAt,
                signingCredentials: creds
            );

            return Ok(new LoginResponse(
                new JwtSecurityTokenHandler().WriteToken(token),
                expiresAt,
                ToMemberSummary(member)));
        }

        // ============================================================
        // POST /api/auth/register - Đăng ký tài khoản mới (public, không cần auth)
        // ============================================================
        // Tài khoản được tạo với Role="Member", SkillPoint=30 (default).
        // Trùng tên sẽ bị từ chối (Name dùng làm username trong hệ thống này).
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            if (!ModelState.IsValid)
                return ValidationProblem(ModelState);

            var trimmedName = request.Name.Trim();

            // Kiểm tra tên đã tồn tại chưa (case-insensitive)
            var exists = await _context.Members
                .AnyAsync(m => m.Name.ToLower() == trimmedName.ToLower());

            if (exists)
                return Conflict("Tên tài khoản đã tồn tại. Vui lòng chọn tên khác.");

            var member = new Member
            {
                Id           = Guid.NewGuid(),
                Name         = trimmedName,
                Role         = "Member",
                SkillPoint   = 30,
                Balance      = 0,
                PasswordHash = _passwordService.HashPassword(request.Password.Trim()),
            };

            _context.Members.Add(member);
            await _context.SaveChangesAsync();

            return Ok(new { Message = "Đăng ký thành công! Liên hệ Admin để kích hoạt tài khoản.", MemberName = member.Name });
        }

        // ============================================================
        // POST /api/auth/change-password - Người dùng tự đổi mật khẩu
        // ============================================================
        // Interview note:
        //   "Tại sao endpoint đổi password phải bắt nhập current password?"
        //   -> Vì nếu chỉ cần JWT + new password, attacker chiếm được token sẽ đổi mật khẩu
        //      nạn nhân rất dễ dàng. Bắt current password tăng thêm 1 lớp xác minh.
        [HttpPost("change-password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            if (!ModelState.IsValid)
                return ValidationProblem(ModelState);

            var memberIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(memberIdStr, out var memberId))
                return Unauthorized("Token không hợp lệ hoặc thiếu thông tin định danh");

            var member = await _context.Members.FindAsync(memberId);
            if (member == null)
                return NotFound("Không tìm thấy tài khoản");

            if (!member.HasPasswordConfigured() || !_passwordService.VerifyPassword(request.CurrentPassword.Trim(), member.PasswordHash!))
                return BadRequest("Mật khẩu hiện tại không đúng");

            if (request.CurrentPassword.Trim() == request.NewPassword.Trim())
                return BadRequest("Mật khẩu mới phải khác mật khẩu hiện tại");

            member.PasswordHash = _passwordService.HashPassword(request.NewPassword.Trim());
            await _context.SaveChangesAsync();

            return Ok(new { Message = "Đổi mật khẩu thành công" });
        }

        private static MemberSummary ToMemberSummary(Member member) =>
            new(member.Id, member.Name, member.SkillPoint, member.Balance, member.Role);
    }
}
