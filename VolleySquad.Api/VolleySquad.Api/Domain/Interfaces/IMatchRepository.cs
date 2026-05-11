// ============================================================
// IMATCH REPOSITORY - Repository Interface cho Match
// ============================================================
// REPOSITORY PATTERN: Trừu tượng hóa tầng data access.
//
// Lợi ích khi dùng Interface thay vì DbContext trực tiếp:
//   1. TESTABILITY: Trong unit test, inject MockMatchRepository thay DbContext thật.
//      Không cần SQL Server để test business logic.
//   2. SWAP IMPLEMENTATION: Muốn chuyển từ SQL Server → MongoDB?
//      Chỉ cần implement MongoMatchRepository : IMatchRepository.
//      Controller/Service KHÔNG cần thay đổi.
//   3. SEPARATION OF CONCERNS: Service không biết đến EF Core, chỉ biết interface.
//
// HIỆN TẠI: Project dùng DbContext trực tiếp trong Controller (Active Record approach).
// Interface này là bản thiết kế cho bước tiếp theo nếu muốn full Clean Architecture.
//
// INTERVIEW NOTE:
// "Repository Pattern vs Unit of Work vs Active Record":
//   Active Record  = Entity tự truy cập DB (Ruby on Rails kiểu cũ).
//   Repository     = Tách riêng data access ra khỏi Entity.
//   Unit of Work   = Gom nhiều repository operations thành 1 transaction (DbContext đã là UoW).
using VolleySquad.Api.Domain;

namespace VolleySquad.Api.Domain.Interfaces
{
    public interface IMatchRepository
    {
        // ============================================================
        // EAGER LOADING vs LAZY LOADING - Minh họa qua interface design
        // ============================================================
        // Hai method khác nhau thể hiện rõ INTENT của caller:

        /// <summary>
        /// Lấy Match theo Id. Không load navigation properties.
        /// Dùng khi chỉ cần thông tin cơ bản của Match (list, overview).
        /// SQL: SELECT * FROM Matches WHERE Id = @id
        /// </summary>
        Task<Match?> GetByIdAsync(Guid id);

        /// <summary>
        /// Lấy Match kèm tất cả SlotTransfers và thông tin Member liên quan.
        /// EAGER LOADING: Dùng .Include().ThenInclude() — 1 SQL query với JOIN.
        /// Dùng khi cần hiển thị chi tiết trận (ai đang pending transfer, ai from/to...).
        /// SQL: SELECT m.*, st.*, fm.*, tm.* FROM Matches m
        ///      LEFT JOIN SlotTransfers st ON st.MatchId = m.Id
        ///      LEFT JOIN Members fm ON fm.Id = st.FromMemberId
        ///      LEFT JOIN Members tm ON tm.Id = st.ToMemberId
        ///      WHERE m.Id = @id
        /// </summary>
        Task<Match?> GetByIdWithTransfersAsync(Guid id);

        /// <summary>
        /// Danh sách tất cả trận, sắp xếp theo ngày thi đấu gần nhất.
        /// Không load navigation properties để tối ưu performance.
        /// </summary>
        Task<List<Match>> GetAllAsync();

        /// <summary>
        /// Danh sách trận của một Member cụ thể (đã đăng ký).
        /// </summary>
        Task<List<Match>> GetMatchesByMemberIdAsync(Guid memberId);

        Task AddAsync(Match match);
        Task<bool> ExistsAsync(Guid id);

        // Unit of Work: SaveChangesAsync() được tách riêng để service
        // có thể group nhiều thao tác vào 1 transaction.
        Task SaveChangesAsync();
    }
}
