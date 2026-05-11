// ============================================================
// MEMBER SERVICE - Implementation của IMemberService
// ============================================================
// SERVICE LIFETIME: AddScoped (đăng ký trong Program.cs)
//
// Scoped = 1 instance mới cho MỖI HTTP Request.
// Request đến → DI Container tạo MemberService → inject AppDbContext (cùng scope)
// Request xong → Cả MemberService lẫn AppDbContext bị Dispose.
//
// KHÔNG dùng AddSingleton vì:
//   AppDbContext là Scoped. Singleton MemberService giữ reference đến Scoped DbContext.
//   Sau request đầu tiên, DbContext bị Dispose nhưng Singleton vẫn giữ nó.
//   Request tiếp theo: "Cannot access a disposed context." → Crash!
//   Đây gọi là CAPTIVE DEPENDENCY (anti-pattern).
//
// KHÔNG dùng AddTransient vì:
//   Transient tạo instance MỚI mỗi lần inject.
//   Nếu Controller và một class khác cùng inject IMemberService trong 1 request,
//   chúng nhận 2 instance khác nhau → mất lợi ích Unit of Work của DbContext.
using Microsoft.EntityFrameworkCore;
using VolleySquad.Api.Domain;
using VolleySquad.Api.Domain.Exceptions;
using VolleySquad.Api.Infrastructure;
using VolleySquad.Api.Services.Interfaces;

namespace VolleySquad.Api.Services
{
    // ============================================================
    // CONSTRUCTOR INJECTION - Cách inject dependency khuyến nghị nhất
    // ============================================================
    // Thay vì Property Injection (public AppDbContext Context { get; set; })
    // hay Method Injection (void SetContext(AppDbContext ctx)),
    // Constructor Injection:
    //   + Dependency rõ ràng: Nhìn constructor biết ngay class này cần gì.
    //   + Immutable: Dependencies được set 1 lần, không thể thay đổi sau đó.
    //   + Testable: Dễ tạo object trong unit test (chỉ cần new MemberService(mockCtx)).
    //   + Fail-fast: Nếu thiếu dependency, lỗi xảy ra khi app start, không phải khi chạy.
    public class MemberService : IMemberService
    {
        // readonly: Compiler đảm bảo field này không bị reassign sau constructor.
        // Quan trọng trong concurrent environment: tránh race condition.
        private readonly AppDbContext _context;
        private readonly ILogger<MemberService> _logger;

        public MemberService(AppDbContext context, ILogger<MemberService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<Member>> GetAllAsync()
        {
            // AsNoTracking(): EF Core KHÔNG theo dõi thay đổi của các entity này.
            // Tối ưu cho READ-ONLY query: tiết kiệm bộ nhớ và tăng tốc độ.
            // Chỉ dùng AsNoTracking khi KHÔNG có ý định update entity đó sau.
            // So sánh:
            //   Có tracking:    Load 1000 members → EF giữ snapshot của tất cả trong RAM
            //   Không tracking: Load 1000 members → Chỉ giữ data, không giữ snapshot
            return await _context.Members
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<Member?> GetByIdAsync(Guid id)
        {
            // FindAsync vs FirstOrDefaultAsync:
            //   FindAsync: Tìm theo PK, kiểm tra Identity Map Cache trước (tránh query DB nếu đã load).
            //              Chỉ dùng được với PK. Return type là T? (không dùng AsNoTracking được).
            //   FirstOrDefaultAsync: Luôn query DB, dùng được với bất kỳ điều kiện nào.
            // Dùng FindAsync khi tìm theo PK và có thể cần update entity sau.
            return await _context.Members.FindAsync(id);
        }

        public async Task<Member> AddMemberAsync(Member member)
        {
            // Server-side validation thay vì tin vào client
            member.Id = Guid.NewGuid();

            // Whitelist validation: chỉ chấp nhận giá trị trong tập hợp đã biết
            if (member.Role != "Admin" && member.Role != "Member")
                member.Role = "Member";

            // Validate SkillPoint nằm trong range hợp lệ
            member.SkillPoint = Math.Clamp(member.SkillPoint, Member.MinSkillPoint, Member.MaxSkillPoint);

            // AnyAsync: Hiệu quả hơn FirstOrDefault khi chỉ cần kiểm tra tồn tại.
            // SQL: SELECT CASE WHEN EXISTS (SELECT 1 FROM Members WHERE Name = @name) THEN 1 ELSE 0 END
            var nameExists = await _context.Members.AnyAsync(m => m.Name == member.Name);
            if (nameExists)
                throw new DomainException($"Thành viên với tên '{member.Name}' đã tồn tại.", "DUPLICATE_NAME");

            _context.Members.Add(member);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Member {Name} (Id: {Id}) added successfully.", member.Name, member.Id);
            return member;
        }

        public async Task DepositAsync(Guid memberId, decimal amount)
        {
            // FindAsync: Tracking entity để EF Core detect thay đổi khi SaveChanges
            var member = await _context.Members.FindAsync(memberId)
                ?? throw new DomainException($"Không tìm thấy thành viên Id: {memberId}");

            // Domain method tự validate và throw DomainException nếu amount <= 0
            member.Deposit(amount);

            // EF Core Change Tracker detect member.Balance đã thay đổi
            // → SaveChanges tự sinh UPDATE Members SET Balance = @newBalance WHERE Id = @id
            await _context.SaveChangesAsync();
        }

        public async Task DeductAsync(Guid memberId, decimal amount)
        {
            var member = await _context.Members.FindAsync(memberId)
                ?? throw new DomainException($"Không tìm thấy thành viên Id: {memberId}");

            // Domain method throw DomainException nếu không đủ số dư.
            // Controller catch DomainException → trả 400 Bad Request.
            // Không cần viết if-else kiểm tra balance ở đây.
            member.Deduct(amount);

            await _context.SaveChangesAsync();
        }

        public async Task ApplyMatchResultAsync(Guid memberId, bool isWinner, int winPoints = 5, int losePoints = 3)
        {
            var member = await _context.Members.FindAsync(memberId)
                ?? throw new DomainException($"Không tìm thấy thành viên Id: {memberId}");

            var oldSkill = member.SkillPoint;

            // Domain method xử lý cả logic + clamp trong [1, 100]
            member.ApplyMatchResult(isWinner, winPoints, losePoints);

            await _context.SaveChangesAsync();

            _logger.LogDebug("SkillPoint updated: Member {Name} {Old} → {New} ({Result})",
                member.Name, oldSkill, member.SkillPoint, isWinner ? "WIN" : "LOSE");
        }

        public async Task<List<Member>> GetLeaderboardAsync(int topN = 20)
        {
            // OrderByDescending trong LINQ → sinh ORDER BY SkillPoint DESC trong SQL.
            // Take(topN) → sinh TOP n trong SQL Server (hoặc LIMIT n trong PostgreSQL).
            // Toàn bộ sort + limit xảy ra tại DB, không load hết về C# rồi sort.
            // AsNoTracking: Read-only, không cần tracking.
            return await _context.Members
                .AsNoTracking()
                .OrderByDescending(m => m.SkillPoint)
                .Take(topN)
                .ToListAsync();
        }
    }
}
