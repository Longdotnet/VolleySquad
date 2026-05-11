// ============================================================
// AUTH CONTROLLER - Xử lý đăng nhập và cấp JWT Token
// ============================================================
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.RateLimiting;
using VolleySquad.Api.Infrastructure;

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

        public AuthController(AppDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        // ============================================================
        // DTO (Data Transfer Object) - Đối tượng truyền dữ liệu
        // ============================================================
        // Dùng record thay vì class vì record immutable (bất biến) — phù hợp với input.
        // KHÔNG dùng trực tiếp entity Member để tránh lộ các field nhạy cảm (Balance, Role...).
        // record trong C# tự generate: constructor, ToString, Equals, GetHashCode.
        public record LoginRequest(string Username);

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
            if (string.IsNullOrWhiteSpace(request.Username))
                return BadRequest("Tên đăng nhập không được để trống");

            // ============================================================
            // FIX 2: Tìm member trong DB để lấy Role và Id thật
            // ============================================================
            // BUG CŨ: username == "admin" => admin. Bất kỳ ai đặt tên "admin" thành Admin!
            // FIX: Đọc Role từ DB — chỉ ai được Admin tạo với Role="Admin" mới có quyền Admin.
            //
            // FirstOrDefaultAsync: Trả về phần tử đầu tiên hoặc null nếu không tìm thấy.
            // Luôn dùng bản Async khi query DB để không block thread (xem giải thích async ở trên).
            var member = await _context.Members
                .FirstOrDefaultAsync(m => m.Name.ToLower() == request.Username.ToLower());

            // ⚠️ LƯU Ý PHỎNG VẤN: App thực tế cần kiểm tra PASSWORD (hash bằng bcrypt/Argon2).
            // Project này bỏ qua vì DB không có trường Password - chỉ để demo kiến trúc JWT.
            if (member == null)
                return Unauthorized("Tên đăng nhập không tồn tại trong hệ thống");

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
                new Claim(ClaimTypes.Role, member.Role)                     // FIX: Role từ DB
            };

            // FIX 4: Đọc SecretKey từ IConfiguration thay vì hardcode
            // Hardcode trong code => lộ key khi push lên GitHub => attacker tạo được token giả
            var secretKey = _configuration["JwtSettings:SecretKey"]!;
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                claims: claims,
                // FIX 5: UtcNow thay vì Now
                // JWT spec (RFC 7519) yêu cầu timestamp theo UTC.
                // Nếu dùng Now và server ở múi giờ +7, token có thể bị tính sai giờ hết hạn.
                expires: DateTime.UtcNow.AddDays(1),
                signingCredentials: creds
            );

            return Ok(new
            {
                token = new JwtSecurityTokenHandler().WriteToken(token),
                expires = DateTime.UtcNow.AddDays(1),
                role = member.Role,
                memberId = member.Id
            });
        }
    }
}
