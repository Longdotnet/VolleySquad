// ============================================================
// ITEAM SERVICE - Service Interface cho Team Splitting
// ============================================================
// Tại sao cần Interface cho Service?
//
// 1. DEPENDENCY INVERSION PRINCIPLE (DIP - chữ D trong SOLID):
//    High-level modules (Controller) không phụ thuộc vào Low-level module (TeamService cụ thể).
//    Cả hai phụ thuộc vào Abstraction (ITeamService).
//    → Controller inject ITeamService, không biết TeamService tồn tại.
//
// 2. TESTABILITY:
//    Trong unit test: inject MockTeamService : ITeamService thay TeamService thật.
//    Không cần tạo Member thật, không cần DB.
//    var mockService = new Mock<ITeamService>();
//    mockService.Setup(s => s.BalanceTeams(It.IsAny<List<Member>>())).Returns(fakeResult);
//
// 3. SWAP IMPLEMENTATION:
//    Muốn đổi từ Snake Draft → ELO-based algorithm?
//    Tạo EloTeamService : ITeamService và đổi registration trong Program.cs.
//    Controller KHÔNG cần thay đổi.
//
// 4. INTERVIEW PATTERN: "Program to an interface, not an implementation" (GoF Design Principle)
using VolleySquad.Api.Domain;
using VolleySquad.Api.Domain.ValueObjects;

namespace VolleySquad.Api.Services.Interfaces
{
    public interface ITeamService
    {
        /// <summary>
        /// Chia danh sách players thành 3 đội cân bằng nhất có thể.
        /// Trả về TeamResult chứa 3 đội + computed metrics.
        /// </summary>
        TeamResult BalanceTeams(List<Member> players);

        /// <summary>
        /// Chia nhóm players đã đăng ký trong 1 trận thành 3 đội.
        /// Overload nhận matchId thay vì danh sách member để đơn giản hóa call site.
        /// Implementation sẽ query DB lấy registered members.
        /// </summary>
        Task<TeamResult> BalanceTeamsByMatchAsync(Guid matchId);
    }
}
