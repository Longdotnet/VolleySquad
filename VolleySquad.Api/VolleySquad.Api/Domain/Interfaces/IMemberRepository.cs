// ============================================================
// IMEMBER REPOSITORY - Repository Interface cho Member
// ============================================================
// Xem IMatchRepository.cs để hiểu Repository Pattern.
//
// SPECIFICATION PATTERN (nâng cao):
// Thay vì có method GetByName(), GetByRole(), GetByMinSkill()... riêng lẻ,
// dùng GetBySpecificationAsync(ISpecification<Member> spec):
//   var spec = new ActiveMembersWithMinSkillSpec(minSkill: 70);
//   var topPlayers = await _repo.GetBySpecificationAsync(spec);
//
// Cách này tránh Repository Interface bị "fat" với hàng chục GetByXxx() methods.
// Tuy nhiên, với project nhỏ-vừa, các method cụ thể vẫn OK và dễ đọc hơn.
using VolleySquad.Api.Domain;

namespace VolleySquad.Api.Domain.Interfaces
{
    public interface IMemberRepository
    {
        Task<Member?> GetByIdAsync(Guid id);

        /// <summary>
        /// Lấy Member kèm tất cả SlotTransfer đã gửi và nhận.
        /// EAGER LOADING: .Include(m => m.OutgoingTransfers).Include(m => m.IncomingTransfers)
        /// Dùng cho trang "Lịch sử chuyển slot" của Member.
        /// </summary>
        Task<Member?> GetByIdWithTransfersAsync(Guid id);

        /// <summary>
        /// Lấy danh sách Members theo danh sách Id (dùng trong Ranking, Payment Worker).
        /// Thay vì N query riêng lẻ, dùng 1 query WHERE Id IN (@ids).
        /// Tránh N+1 Problem: KHÔNG gọi GetByIdAsync() trong vòng for loop.
        /// </summary>
        Task<List<Member>> GetByIdsAsync(IEnumerable<Guid> ids);

        Task<List<Member>> GetAllAsync();

        /// <summary>
        /// Lấy danh sách Member đã đăng ký trong 1 trận cụ thể.
        /// Dùng trong TeamService để chia đội từ người đã đăng ký.
        /// </summary>
        Task<List<Member>> GetRegisteredMembersAsync(Guid matchId);

        Task<bool> ExistsByIdAsync(Guid id);

        /// <summary>
        /// Kiểm tra Name đã tồn tại chưa (dùng khi add-member để tránh trùng tên).
        /// AnyAsync() hiệu quả hơn GetByName(): SELECT TOP 1 1 vs SELECT *
        /// </summary>
        Task<bool> ExistsByNameAsync(string name);

        Task AddAsync(Member member);
        Task SaveChangesAsync();
    }
}
