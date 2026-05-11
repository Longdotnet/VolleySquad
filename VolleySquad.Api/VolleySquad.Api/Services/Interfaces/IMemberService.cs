// ============================================================
// IMEMBER SERVICE - Service Interface cho Member operations
// ============================================================
// Service Layer nằm giữa Controller và Repository/DbContext.
// Controller: Nhận HTTP request, validate input, gọi Service.
// Service:    Orchestrate business logic, gọi Repository, publish events.
// Repository: Truy vấn và lưu trữ data.
//
// SERVICE LIFETIME cho MemberService: AddScoped
// Lý do: MemberService phụ thuộc AppDbContext (Scoped).
// Nếu đăng ký Singleton → Captive Dependency (lỗi phổ biến):
//   Singleton sống xuyên suốt app. Nó giữ reference đến Scoped DbContext đã Disposed.
//   Kết quả: ObjectDisposedException, stale data, thread-safety issues.
using VolleySquad.Api.Domain;

namespace VolleySquad.Api.Services.Interfaces
{
    public interface IMemberService
    {
        Task<List<Member>> GetAllAsync();
        Task<Member?> GetByIdAsync(Guid id);

        /// <summary>
        /// Thêm thành viên mới. Service tự validate: tên không trùng, role hợp lệ.
        /// Tách logic validate khỏi Controller để reusable.
        /// </summary>
        Task<Member> AddMemberAsync(Member member);

        /// <summary>
        /// Nạp tiền cho Member. Dùng domain method member.Deposit() thay vì set thẳng Balance.
        /// Lý do: Domain method validate amount > 0, đảm bảo invariant.
        /// </summary>
        Task DepositAsync(Guid memberId, decimal amount);

        /// <summary>
        /// Trừ tiền từ tài khoản (Payment). Throw DomainException nếu không đủ số dư.
        /// Payment.Worker gọi method này thay vì thao tác DB trực tiếp.
        /// </summary>
        Task DeductAsync(Guid memberId, decimal amount);

        /// <summary>
        /// Cập nhật SkillPoint sau trận. Ranking.Worker gọi method này.
        /// Dùng member.ApplyMatchResult() để đảm bảo giới hạn 1-100.
        /// </summary>
        Task ApplyMatchResultAsync(Guid memberId, bool isWinner, int winPoints = 5, int losePoints = 3);

        /// <summary>
        /// Lấy leaderboard — danh sách members sắp xếp giảm dần theo SkillPoint.
        /// Thay vì GetAll().OrderBy() ở Controller, tập trung sort logic ở đây.
        /// Tối ưu: Sort tại DB (ORDER BY trong SQL) thay vì load all rồi sort ở C#.
        /// </summary>
        Task<List<Member>> GetLeaderboardAsync(int topN = 20);
    }
}
